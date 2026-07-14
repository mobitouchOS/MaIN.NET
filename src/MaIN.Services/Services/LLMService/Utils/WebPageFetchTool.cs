using System.Text.Json;
using System.Text.RegularExpressions;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class WebPageFetchTool
{
    public const string Name = "fetch_web_page";
    public const string AliasName = "read_url";

    private static readonly HttpClient s_defaultHttpClient = CreateDefaultHttpClient();

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    public static ToolDefinition Create(IHttpClientFactory? httpClientFactory = null, string toolName = Name, CancellationToken ct = default)
    {
        return new ToolDefinition
        {
            Type = "function",
            IsClientSide = false,
            Function = new()
            {
                Name = toolName,
                Description = """
                    Fetch the text content of a public web page URL.
                    Call this tool when you have a specific website URL or link and need to read its content or documentation.
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new
                        {
                            type = "string",
                            description = "The absolute HTTP or HTTPS URL of the web page to fetch."
                        }
                    },
                    required = new[] { "url" }
                }
            },
            Execute = argsJson => ExecuteAsync(httpClientFactory, argsJson, ct)
        };
    }

    private static async Task<string> ExecuteAsync(IHttpClientFactory? httpClientFactory, string argsJson, CancellationToken ct)
    {
        var url = ExtractUrl(argsJson);
        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return "Error: Invalid or missing HTTP/HTTPS URL provided.";
        }

        try
        {
            var client = httpClientFactory?.CreateClient() ?? s_defaultHttpClient;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (httpClientFactory is not null && !request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return $"Failed to fetch URL {url}. HTTP status code: {(int)response.StatusCode}";
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var cleanText = StripHtmlAndScripts(html);

            if (cleanText.Length > 12000)
            {
                cleanText = cleanText.Substring(0, 12000) + "\n\n[Content truncated at 12,000 characters]";
            }

            return $"Content of {url}:\n\n{cleanText}";
        }
        catch (Exception ex)
        {
            return $"Error fetching web page '{url}': {ex.Message}";
        }
    }

    private static string StripHtmlAndScripts(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var text = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<!--[\s\S]*?-->", " ");
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string ExtractUrl(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("url", out var urlElement))
            {
                return urlElement.GetString() ?? argsJson;
            }
        }
        catch { }
        return argsJson;
    }
}
