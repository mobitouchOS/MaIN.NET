using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using MaIN.Services.Services.PaddleOcr;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Page = UglyToad.PdfPig.Content.Page;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace MaIN.Services.Services.LLMService.Memory;

public static class DocumentProcessor
{
    public static string ProcessDocument(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".pdf" => ProcessPdf(filePath),
            ".docx" => ProcessDocx(filePath),
            ".xlsx" or ".xls" => ProcessExcel(filePath),
            ".jpg" or ".jpeg" or ".png" or ".tiff" or ".bmp" => ProcessImage(filePath),
            ".txt" => ProcessTextFile(filePath),
            ".rtf" => ProcessRtf(filePath),
            ".html" or ".htm" => ProcessHtml(filePath),
            ".json" or ".md" => ProcessDefault(filePath),
            _ => throw new NotSupportedException($"Format {extension} not supported")
        };
    }

    public static async Task<string[]> ConvertToFilesContent(ChatMemoryOptions options, CancellationToken cancellationToken = default)
    {
        var files = new List<string>();
        foreach (var fData in options.FilesData)
        {
            files.Add(fData.Value);
        }

        foreach (var sData in options.StreamData)
        {
            var path = Path.Combine(Path.GetTempPath(), sData.Key);
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            await sData.Value.CopyToAsync(fileStream, cancellationToken);
            files.Add(path);
        }

        foreach (var txt in options.TextData)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{txt.Key}.txt");
            await File.WriteAllTextAsync(path, txt.Value, cancellationToken);
            files.Add(path);
        }

        if (options.WebUrls.Count <= 0) return files.ToArray();
        {
            using HttpClient client = new HttpClient();
            foreach (var web in options.WebUrls)
            {
                var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
                var html = await client.GetStringAsync(web, cancellationToken);
                await File.WriteAllTextAsync(path, html, cancellationToken);
                files.Add(path);
            }
        }

        return files.ToArray();
    }
    
    private static string ProcessDefault(string filePath)
    {
        var file = File.ReadAllText(filePath);
        return file;
    }
    
    private static string ProcessPdf(string pdfPath)
    {
        var result = new StringBuilder();

        using (var document = PdfDocument.Open(pdfPath))
        {
            foreach (var page in document.GetPages())
            {
                var pageText = ExtractPageText(page);
                result.Append(pageText);
            }
        }

        return result.ToString();
    }

    private static string ExtractPageText(Page page)
    {
        var words = page.GetWords().ToList();
        var sb = new StringBuilder();

        var rows = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
            .OrderByDescending(g => g.Key)
            .ToList();

        foreach (var row in rows)
        {
            var lineWords = row.OrderBy(w => w.BoundingBox.Left).ToList();
            string line = string.Join(" ", lineWords.Select(w => w.Text)).Trim();

            if (string.IsNullOrWhiteSpace(line)) continue;

            if (IsPotentialHeader(lineWords))
            {
                sb.AppendLine($"# {line}");
            }
            else if (IsLabelValuePair(line))
            {
                var parts = SplitLabelValue(line);
                sb.AppendLine($"{parts.Item1}: {parts.Item2}");
            }
            else if (IsListItem(line))
            {
                sb.AppendLine($"- {line}");
            }
            else if (IsDataRow(line))
            {
                sb.AppendLine(FormatDataRowConcise(line));
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    private static bool IsPotentialHeader(List<Word> words)
    {
        if (!words.Any()) return false;

        var firstWord = words.First();
        double fontSize = 0;

        if (firstWord.Letters.Any())
        {
            fontSize = firstWord.Letters.First().FontSize;
        }
        else
        {
            fontSize = firstWord.BoundingBox.Height;
        }

        bool isBold = firstWord.Letters.Any() &&
                      firstWord.Letters.First().FontName!.ToLower().Contains("bold");

        return fontSize > 12 || isBold;
    }

    private static bool IsLabelValuePair(string line)
    {
        return Regex.IsMatch(line, @"^.+:.+$");
    }

    private static Tuple<string, string> SplitLabelValue(string line)
    {
        var parts = line.Split([':'], 2);

        if (parts.Length == 2)
        {
            return new Tuple<string, string>(parts[0].Trim(), parts[1].Trim());
        }

        return new Tuple<string, string>(line, "");
    }

    private static bool IsListItem(string line)
    {
        return line.TrimStart().StartsWith("•") ||
               line.TrimStart().StartsWith("-") ||
               line.TrimStart().StartsWith("*") ||
               Regex.IsMatch(line.TrimStart(), @"^\d+\.\s");
    }

    private static bool IsDataRow(string line)
    {
        return ContainsNumberWithUnit(line) && Regex.Matches(line, @"\b\d+([.,]\d+)?\b").Count >= 2;
    }

    private static bool ContainsNumberWithUnit(string line)
    {
        return Regex.IsMatch(line, @"\b\d+\s*[a-zA-Z]{1,3}\b");
    }

    private static string FormatDataRowConcise(string line)
    {
        var textMatch = Regex.Match(line, @"^(.*?)\s*\d");
        string descriptorText = textMatch.Success ? textMatch.Groups[1].Value.Trim() : "";

        var numUnitMatches = Regex.Matches(line, @"\b(\d+)\s*([a-zA-Z]{1,3})\b");

        var numberMatches = Regex.Matches(line, @"\b(\d+([.,]\d+)?)\b");

        var sb = new StringBuilder();
        sb.Append("- ");
        sb.Append(descriptorText);

        if (numUnitMatches.Count > 0)
        {
            foreach (Match m in numUnitMatches)
            {
                sb.Append($" | {m.Groups[1].Value} {m.Groups[2].Value}");
            }
        }

        var processedIndices = new HashSet<int>();
        foreach (Match numUnitMatch in numUnitMatches)
        {
            foreach (Match numMatch in numberMatches)
            {
                if (numMatch.Index >= numUnitMatch.Index &&
                    numMatch.Index < numUnitMatch.Index + numUnitMatch.Length)
                {
                    processedIndices.Add(numMatch.Index);
                }
            }
        }

        foreach (Match numMatch in numberMatches)
        {
            if (!processedIndices.Contains(numMatch.Index))
            {
                sb.Append($" | {numMatch.Value}");
            }
        }

        return sb.ToString();
    }


    private static string ProcessExcel(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[SPREADSHEET_START]");

        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = document.WorkbookPart;
        var sheets = workbookPart!.Workbook.Descendants<Sheet>();
        var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var sheet in sheets)
        {
            var worksheetPart = (WorksheetPart)workbookPart!.GetPartById(sheet.Id!);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            structuredContent.AppendLine($"[SHEET:{sheet.Name}]");

            if (!sheetData.Elements<Row>().Any())
            {
                structuredContent.AppendLine("[EMPTY_SHEET]");
                structuredContent.AppendLine($"[/SHEET:{sheet.Name}]");
                continue;
            }

            bool firstRow = true;
            int maxColumns = 0;

            foreach (var row in sheetData.Elements<Row>())
            {
                int cellCount = row.Elements<Cell>().Count();
                maxColumns = Math.Max(maxColumns, cellCount);
            }

            foreach (var row in sheetData.Elements<Row>())
            {
                var rowContent = new StringBuilder("|");
                var cells = row.Elements<Cell>().ToList();

                for (int i = 0; i < maxColumns; i++)
                {
                    string cellValue = "";
                    var cell = cells.FirstOrDefault(c => GetColumnIndex(GetColumnId(c.CellReference!)) == i);

                    if (cell != null)
                    {
                        cellValue = GetCellValue(cell, sharedStringTable!);
                    }

                    rowContent.Append($" {cellValue} |");
                }

                structuredContent.AppendLine(rowContent.ToString());

                if (firstRow && maxColumns > 0)
                {
                    var separatorRow = new StringBuilder("|");
                    for (int i = 0; i < maxColumns; i++)
                    {
                        separatorRow.Append(" --- |");
                    }

                    structuredContent.AppendLine(separatorRow.ToString());
                    firstRow = false;
                }
            }

            structuredContent.AppendLine($"[/SHEET:{sheet.Name}]");
        }

        structuredContent.AppendLine("[SPREADSHEET_END]");
        return structuredContent.ToString();
    }

    private static string GetColumnId(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return "";

        return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
    }

    private static int GetColumnIndex(string columnId)
    {
        int index = 0;
        foreach (char c in columnId)
        {
            index = (index * 26) + (c - 'A' + 1);
        }

        return index - 1;
    }

    private static string GetCellValue(Cell cell, SharedStringTable sharedStringTable)
    {
        if (cell.CellValue == null)
            return string.Empty;

        string value = cell.CellValue.Text;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString && sharedStringTable != null)
        {
            if (int.TryParse(value, out int ssid) && ssid >= 0 && ssid < sharedStringTable.Count())
            {
                return sharedStringTable.ElementAt(ssid).InnerText;
            }
        }

        return value;
    }

    private static string ProcessDocx(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[DOCUMENT_START]");

        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document.Body;

        structuredContent.AppendLine("[METADATA_START]");
        if (!string.IsNullOrEmpty(document.PackageProperties.Title))
            structuredContent.AppendLine($"Title: {document.PackageProperties.Title}");
        if (!string.IsNullOrEmpty(document.PackageProperties.Creator))
            structuredContent.AppendLine($"Author: {document.PackageProperties.Creator}");
        if (!string.IsNullOrEmpty(document.PackageProperties.Subject))
            structuredContent.AppendLine($"Subject: {document.PackageProperties.Subject}");
        structuredContent.AppendLine("[METADATA_END]");

        foreach (var element in body?.Elements()!)
        {
            if (element is Paragraph paragraph)
            {
                string text = ExtractTextFromParagraph(paragraph);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (IsParagraphHeading(paragraph))
                {
                    structuredContent.AppendLine($"[HEADING]{text}[/HEADING]");
                }
                else
                {
                    structuredContent.AppendLine(text);
                }
            }
            else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
            {
                structuredContent.AppendLine("[TABLE_START]");
                FormatWordTableAsMarkdown(table, structuredContent);
                structuredContent.AppendLine("[TABLE_END]");
            }
        }

        structuredContent.AppendLine("[DOCUMENT_END]");
        return structuredContent.ToString();
    }

    private static string ExtractTextFromParagraph(Paragraph paragraph)
    {
        return string.Join(" ", paragraph.Descendants<Text>().Select(t => t.Text));
    }

    private static bool IsParagraphHeading(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId != null && (styleId.StartsWith("Heading") || styleId.StartsWith("Title"));
    }

    private static void FormatWordTableAsMarkdown(DocumentFormat.OpenXml.Wordprocessing.Table table,
        StringBuilder output)
    {
        bool isFirstRow = true;

        foreach (var row in table.Elements<TableRow>())
        {
            StringBuilder rowBuilder = new StringBuilder("|");

            foreach (var cell in row.Elements<TableCell>())
            {
                string cellText = string.Join(" ", cell.Descendants<Text>().Select(t => t.Text));
                rowBuilder.Append($" {cellText.Trim()} |");
            }

            output.AppendLine(rowBuilder.ToString());

            if (isFirstRow)
            {
                isFirstRow = false;
                int cellCount = row.Elements<TableCell>().Count();
                StringBuilder separatorBuilder = new StringBuilder("|");

                for (int i = 0; i < cellCount; i++)
                {
                    separatorBuilder.Append(" --- |");
                }

                output.AppendLine(separatorBuilder.ToString());
            }
        }
    }

    private static string ProcessImage(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[IMAGE_DOCUMENT_START]");

        try
        {
            string text = PaddleOcrProvider.ExtractFromFileAsync(filePath).GetAwaiter().GetResult();
            var lines = text.Split('\n');
            bool inTable = false;

            foreach (var line in lines)
            {
                bool looksLikeTableRow = line.Contains('\t') || line.Contains("   ");

                if (looksLikeTableRow && !inTable)
                {
                    structuredContent.AppendLine("[TABLE_START]");
                    inTable = true;
                }
                else if (!looksLikeTableRow && inTable)
                {
                    structuredContent.AppendLine("[TABLE_END]");
                    inTable = false;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (inTable)
                    {
                        string formattedLine = line.Trim();
                        formattedLine = Regex.Replace(formattedLine, @"\s{3,}", " | ");
                        formattedLine = formattedLine.Replace('\t', '|');
                        structuredContent.AppendLine($"|{formattedLine}|");
                    }
                    else
                    {
                        structuredContent.AppendLine(line);
                    }
                }
            }

            if (inTable)
            {
                structuredContent.AppendLine("[TABLE_END]");
            }
        }
        catch (Exception ex)
        {
            structuredContent.AppendLine($"[OCR_ERROR: {ex.Message}]");
        }

        structuredContent.AppendLine("[IMAGE_DOCUMENT_END]");
        return structuredContent.ToString();
    }

    private static string ProcessTextFile(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[TEXT_DOCUMENT_START]");

        string[] lines = File.ReadAllLines(filePath);
        bool inTable = false;

        foreach (var line in lines)
        {
            bool looksLikeTableRow = IsLikelyTableRow(line);

            if (looksLikeTableRow && !inTable)
            {
                structuredContent.AppendLine("[TABLE_START]");
                inTable = true;
            }
            else if (!looksLikeTableRow && inTable)
            {
                structuredContent.AppendLine("[TABLE_END]");
                inTable = false;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                if (inTable)
                {
                    string formattedLine = FormatPlainTextTableRow(line);
                    structuredContent.AppendLine(formattedLine);
                }
                else
                {
                    structuredContent.AppendLine(line);
                }
            }
        }

        if (inTable)
        {
            structuredContent.AppendLine("[TABLE_END]");
        }

        structuredContent.AppendLine("[TEXT_DOCUMENT_END]");
        return structuredContent.ToString();
    }

    private static bool IsLikelyTableRow(string line)
    {
        if (line.Count(c => c == '\t') >= 2)
            return true;

        var spaces = new List<int>();
        int currentSpace = 0;

        foreach (var t in line)
        {
            if (t == ' ')
            {
                currentSpace++;
            }
            else
            {
                if (currentSpace >= 3)
                {
                    spaces.Add(currentSpace);
                }

                currentSpace = 0;
            }
        }

        if (currentSpace >= 3)
        {
            spaces.Add(currentSpace);
        }

        return spaces.Count >= 2;
    }

    private static string FormatPlainTextTableRow(string line)
    {
        string formatted = line.Replace('\t', '|');
        formatted = Regex.Replace(formatted, @"\s{3,}", "|");

        if (!formatted.StartsWith("|"))
            formatted = "|" + formatted;

        if (!formatted.EndsWith("|"))
            formatted = formatted + "|";

        return formatted;
    }

    private static string ProcessRtf(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[RTF_DOCUMENT_START]");

        try
        {
            string rtfText = File.ReadAllText(filePath);
            string plainText = ConvertRtfToPlainText(rtfText);
            string[] lines = plainText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    structuredContent.AppendLine(line.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            structuredContent.AppendLine($"[RTF_PROCESSING_ERROR: {ex.Message}]");
        }

        structuredContent.AppendLine("[RTF_DOCUMENT_END]");
        return structuredContent.ToString();
    }

    private static string ConvertRtfToPlainText(string rtfText)
    {
        string plainText = rtfText;
        int headerEnd = plainText.IndexOf("\\viewkind4", StringComparison.Ordinal);

        if (headerEnd > 0)
        {
            plainText = plainText.Substring(headerEnd);
        }

        plainText = Regex.Replace(plainText, @"\\[a-zA-Z]+[0-9]*", " ");
        plainText = plainText.Replace("{", "").Replace("}", "");
        plainText = plainText.Replace("\\", "");
        plainText = Regex.Replace(plainText, @"\s+", " ");

        return plainText.Trim();
    }

    private static string ProcessHtml(string filePath)
    {
        var structuredContent = new StringBuilder();
        structuredContent.AppendLine("[HTML_DOCUMENT_START]");

        try
        {
            string htmlText = File.ReadAllText(filePath);
            string plainText = StripHtmlTags(htmlText);
            ExtractTablesFromHtml(htmlText, structuredContent);

            var lines = plainText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    structuredContent.AppendLine(line.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            structuredContent.AppendLine($"[HTML_PROCESSING_ERROR: {ex.Message}]");
        }

        structuredContent.AppendLine("[HTML_DOCUMENT_END]");
        return structuredContent.ToString();
    }

    private static string StripHtmlTags(string html)
    {
        return Regex.Replace(html, @"<[^>]+>", " ");
    }

    private static void ExtractTablesFromHtml(string html, StringBuilder output)
    {
        var tableMatches = Regex.Matches(html, @"<table[^>]*>(.*?)</table>", RegexOptions.Singleline);

        foreach (Match tableMatch in tableMatches)
        {
            string tableHtml = tableMatch.Groups[1].Value;
            output.AppendLine("[TABLE_START]");
            var rowMatches = Regex.Matches(tableHtml, @"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline);
            bool isFirstRow = true;

            foreach (Match rowMatch in rowMatches)
            {
                string rowHtml = rowMatch.Groups[1].Value;
                StringBuilder rowBuilder = new StringBuilder("|");
                var cellMatches = Regex.Matches(rowHtml, @"<(td|th)[^>]*>(.*?)</(?:td|th)>", RegexOptions.Singleline);

                foreach (Match cellMatch in cellMatches)
                {
                    string cellContent = cellMatch.Groups[2].Value;
                    cellContent = Regex.Replace(cellContent, @"<[^>]+>", "");
                    rowBuilder.Append($" {cellContent.Trim()} |");
                }

                output.AppendLine(rowBuilder.ToString());

                if (isFirstRow)
                {
                    int cellCount = cellMatches.Count;
                    StringBuilder separatorBuilder = new StringBuilder("|");

                    for (int i = 0; i < cellCount; i++)
                    {
                        separatorBuilder.Append(" --- |");
                    }

                    output.AppendLine(separatorBuilder.ToString());
                    isFirstRow = false;
                }
            }

            output.AppendLine("[TABLE_END]");
        }
    }
}