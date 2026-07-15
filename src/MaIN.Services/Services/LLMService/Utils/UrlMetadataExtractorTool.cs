using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class UrlMetadataExtractorTool
{
    public const string Name = "extract_url_metadata";
    private const string Description = "Extracts OpenGraph and HTML header metadata (title, description, author, site_name, image) from a webpage URL without loading the entire full body text.";

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class MetadataArguments
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public static ToolDefinition Create(IHttpClientFactory? httpClientFactory = null, string toolName = Name)
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = toolName,
                Description = Description,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new
                        {
                            type = "string",
                            description = "The absolute URL of the webpage to extract metadata from (e.g. 'https://github.com/mobitouchOS/MaIN.NET')."
                        }
                    },
                    required = new[] { "url" }
                }
            },
            Execute = async (argumentsJson) =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<MetadataArguments>(argumentsJson, s_jsonOptions);
                    if (args is null || string.IsNullOrWhiteSpace(args.Url))
                    {
                        return "{\"error\": \"Missing required parameter 'url' for extract_url_metadata tool.\"}";
                    }

                    if (!Uri.TryCreate(args.Url.Trim(), UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return $"{{\"error\": \"Invalid URL schema. Only HTTP and HTTPS are supported: '{args.Url}'\"}}";
                    }

                    var client = httpClientFactory?.CreateClient("MaIN_UrlMetadataExtractorTool") ?? new HttpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MaIN-Agent", "1.0"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));

                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"{{\"error\": \"Failed to fetch URL. Status code: {(int)response.StatusCode} {response.ReasonPhrase}\"}}";
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                    using var reader = new StreamReader(stream);
                    var buffer = new char[65536];
                    var charsRead = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                    var htmlSnippet = new string(buffer, 0, charsRead);

                    var title = ExtractMetaContent(htmlSnippet, "og:title")
                                ?? ExtractMetaContent(htmlSnippet, "twitter:title")
                                ?? ExtractTitleTag(htmlSnippet)
                                ?? string.Empty;

                    var description = ExtractMetaContent(htmlSnippet, "og:description")
                                      ?? ExtractMetaContent(htmlSnippet, "description")
                                      ?? ExtractMetaContent(htmlSnippet, "twitter:description")
                                      ?? string.Empty;

                    var siteName = ExtractMetaContent(htmlSnippet, "og:site_name")
                                   ?? ExtractMetaContent(htmlSnippet, "application-name")
                                   ?? uri.Host;

                    var image = ExtractMetaContent(htmlSnippet, "og:image")
                                ?? ExtractMetaContent(htmlSnippet, "twitter:image")
                                ?? string.Empty;

                    var author = ExtractMetaContent(htmlSnippet, "author")
                                 ?? ExtractMetaContent(htmlSnippet, "article:author")
                                 ?? string.Empty;

                    var resultObject = new
                    {
                        url = uri.ToString(),
                        title,
                        description,
                        site_name = siteName,
                        author,
                        image
                    };

                    return JsonSerializer.Serialize(resultObject, s_jsonOptions);
                }
                catch (TaskCanceledException)
                {
                    return "{\"error\": \"Metadata extraction timed out after 15 seconds.\"}";
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = $"Failed to extract URL metadata: {ex.Message}" }, s_jsonOptions);
                }
            }
        };
    }

    private static string? ExtractTitleTag(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            return Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim();
        }
        return null;
    }

    private static string? ExtractMetaContent(string html, string propertyOrName)
    {
        var pattern = $@"<meta\s+[^>]*(?:property|name)=[""'](?:[^""']*?{Regex.Escape(propertyOrName)}[^""']*?)[""'][^>]*content=[""']([^""']*)[""'][^>]*>|<meta\s+[^>]*content=[""']([^""']*)[""'][^>]*(?:property|name)=[""'](?:[^""']*?{Regex.Escape(propertyOrName)}[^""']*?)[""'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return Regex.Replace(content, @"\s+", " ").Trim();
        }
        return null;
    }
}
