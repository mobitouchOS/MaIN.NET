using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class WebSearchTool
{
    public const string Name = "web_search";
    public const string PreviewName = "web_search_preview";

    private static readonly HttpClient s_defaultHttpClient = CreateDefaultHttpClient();

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
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
                    Search the web for up-to-date information, current events, articles, and real-time facts.
                    Call this tool whenever the answer depends on information beyond your training cutoff or current public data.
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The natural-language search query to search across the web."
                        }
                    },
                    required = new[] { "query" }
                }
            },
            Execute = argsJson => ExecuteAsync(httpClientFactory, argsJson, ct)
        };
    }

    private static async Task<string> ExecuteAsync(IHttpClientFactory? httpClientFactory, string argsJson, CancellationToken ct)
    {
        var query = ExtractQuery(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: Empty search query provided.";
        }

        try
        {
            var client = httpClientFactory?.CreateClient() ?? s_defaultHttpClient;
            var requestUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (httpClientFactory is not null && !request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            }

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return $"Web search request failed with HTTP status {(int)response.StatusCode} for query: {query}";
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var results = ParseDuckDuckGoHtml(html);

            if (results.Count == 0)
            {
                return $"No web search results found for query: {query}";
            }

            var formatted = string.Join("\n\n", results.Select((r, i) => $"**{i + 1}. {r.Title}**\nURL: {r.Url}\n{r.Snippet}"));
            return $"Web search results for '{query}':\n\n{formatted}";
        }
        catch (Exception ex)
        {
            return $"Web search encountered an error for query '{query}': {ex.Message}";
        }
    }

    private static List<(string Title, string Url, string Snippet)> ParseDuckDuckGoHtml(string html)
    {
        var results = new List<(string Title, string Url, string Snippet)>();

        // DuckDuckGo HTML results typically have elements with class "result__body" containing link/title ("result__a") and snippet ("result__snippet").
        var blockMatches = Regex.Matches(html, @"<div[^>]*class=""[^""]*result__body[^""]*""[^>]*>(.*?)</div>\s*</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match match in blockMatches)
        {
            var block = match.Groups[1].Value;

            var titleMatch = Regex.Match(block, @"<a[^>]*class=""[^""]*result__a[^""]*""[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!titleMatch.Success) continue;

            var rawUrl = titleMatch.Groups[1].Value;
            var rawTitle = StripHtmlTags(titleMatch.Groups[2].Value);

            var snippetMatch = Regex.Match(block, @"<a[^>]*class=""[^""]*result__snippet[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var rawSnippet = snippetMatch.Success ? StripHtmlTags(snippetMatch.Groups[1].Value) : string.Empty;

            var cleanUrl = CleanDuckDuckGoUrl(rawUrl);
            if (string.IsNullOrWhiteSpace(rawTitle) || string.IsNullOrWhiteSpace(cleanUrl)) continue;

            results.Add((rawTitle.Trim(), cleanUrl.Trim(), rawSnippet.Trim()));
            if (results.Count >= 6) break;
        }

        return results;
    }

    private static string CleanDuckDuckGoUrl(string url)
    {
        if (url.Contains("uddg="))
        {
            var match = Regex.Match(url, @"uddg=([^&]+)");
            if (match.Success)
            {
                return Uri.UnescapeDataString(match.Groups[1].Value);
            }
        }
        return url;
    }

    private static string StripHtmlTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var stripped = Regex.Replace(input, @"<[^>]+>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private static string ExtractQuery(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("query", out var queryElement))
            {
                return queryElement.GetString() ?? argsJson;
            }
        }
        catch { }
        return argsJson;
    }
}
