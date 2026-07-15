using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class RssFeedReaderTool
{
    public const string Name = "rss_feed_reader";
    private const string Description = "Fetches and parses an RSS or Atom feed from a URL, returning structured recent articles/items (title, link, published date, summary).";

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class RssArguments
    {
        [JsonPropertyName("feed_url")]
        public string FeedUrl { get; set; } = string.Empty;

        [JsonPropertyName("max_items")]
        public int? MaxItems { get; set; }
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
                        feed_url = new
                        {
                            type = "string",
                            description = "The absolute URL to the RSS or Atom feed XML (e.g. 'https://hnrss.org/frontpage' or 'https://devblogs.microsoft.com/dotnet/feed/')."
                        },
                        max_items = new
                        {
                            type = "integer",
                            description = "Maximum number of recent items/entries to return from the feed. Defaults to 10."
                        }
                    },
                    required = new[] { "feed_url" }
                }
            },
            Execute = async (argumentsJson) =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<RssArguments>(argumentsJson, s_jsonOptions);
                    if (args is null || string.IsNullOrWhiteSpace(args.FeedUrl))
                    {
                        return "{\"error\": \"Missing required parameter 'feed_url' for rss_feed_reader tool.\"}";
                    }

                    if (!Uri.TryCreate(args.FeedUrl.Trim(), UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return $"{{\"error\": \"Invalid feed URL. Must be an absolute HTTP or HTTPS URL: '{args.FeedUrl}'\"}}";
                    }

                    var limit = args.MaxItems ?? 10;
                    if (limit <= 0) limit = 10;
                    if (limit > 30) limit = 30;

                    var client = httpClientFactory?.CreateClient("MaIN_RssFeedReaderTool") ?? new HttpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MaIN-Agent", "1.0"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

                    using var response = await client.SendAsync(request, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"{{\"error\": \"Failed to fetch feed URL. Status code: {(int)response.StatusCode} {response.ReasonPhrase}\"}}";
                    }

                    var xmlContent = await response.Content.ReadAsStringAsync(cts.Token);
                    if (string.IsNullOrWhiteSpace(xmlContent))
                    {
                        return "{\"error\": \"Feed URL returned empty content.\"}";
                    }

                    var doc = XDocument.Parse(xmlContent);
                    var root = doc.Root;
                    if (root is null)
                    {
                        return "{\"error\": \"Invalid XML feed format.\"}";
                    }

                    string feedTitle = string.Empty;
                    string feedDescription = string.Empty;
                    var items = new List<object>();

                    if (string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase) ||
                        root.Elements().Any(e => string.Equals(e.Name.LocalName, "channel", StringComparison.OrdinalIgnoreCase)))
                    {
                        var channel = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "channel", StringComparison.OrdinalIgnoreCase)) ?? root;
                        feedTitle = channel.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                        feedDescription = channel.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "description", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;

                        foreach (var item in channel.Elements().Where(e => string.Equals(e.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase)).Take(limit))
                        {
                            var title = item.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                            var link = item.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                            var pubDate = item.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "pubDate", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                            var summaryXml = item.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "description", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name.LocalName, "encoded", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

                            items.Add(new
                            {
                                title,
                                link,
                                published = pubDate,
                                summary = CleanHtml(summaryXml)
                            });
                        }
                    }
                    else if (string.Equals(root.Name.LocalName, "feed", StringComparison.OrdinalIgnoreCase))
                    {
                        feedTitle = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                        feedDescription = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "subtitle", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;

                        foreach (var entry in root.Elements().Where(e => string.Equals(e.Name.LocalName, "entry", StringComparison.OrdinalIgnoreCase)).Take(limit))
                        {
                            var title = entry.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                            var linkElem = entry.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(e.Attribute("rel")?.Value) || e.Attribute("rel")?.Value == "alternate"))
                                           ?? entry.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase));
                            var link = linkElem?.Attribute("href")?.Value?.Trim() ?? linkElem?.Value?.Trim() ?? string.Empty;
                            var pubDate = entry.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "updated", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name.LocalName, "published", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? string.Empty;
                            var summaryXml = entry.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "summary", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name.LocalName, "content", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

                            items.Add(new
                            {
                                title,
                                link,
                                published = pubDate,
                                summary = CleanHtml(summaryXml)
                            });
                        }
                    }
                    else
                    {
                        return "{\"error\": \"Unsupported XML format: expected RSS or Atom root elements.\"}";
                    }

                    var resultObject = new
                    {
                        feed_title = feedTitle,
                        feed_description = feedDescription,
                        item_count = items.Count,
                        items
                    };

                    return JsonSerializer.Serialize(resultObject, s_jsonOptions);
                }
                catch (TaskCanceledException)
                {
                    return "{\"error\": \"RSS feed request timed out after 15 seconds.\"}";
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = $"Failed to parse RSS feed: {ex.Message}" }, s_jsonOptions);
                }
            }
        };
    }

    private static string CleanHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var stripped = Regex.Replace(input, "<.*?>", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        const int maxLen = 400;
        if (stripped.Length > maxLen)
        {
            stripped = stripped[..maxLen] + "...";
        }
        return stripped;
    }
}
