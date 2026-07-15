using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class HttpRequestTool
{
    public const string Name = "http_request";
    private const string Description = "Makes an HTTP request (GET, POST, PUT, DELETE, PATCH) to an external REST API or web endpoint, allowing custom headers and JSON payload.";

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class HttpRequestArguments
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
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
                            description = "The absolute HTTP or HTTPS URL to make the request to (e.g. 'https://api.example.com/v1/status')."
                        },
                        method = new
                        {
                            type = "string",
                            description = "The HTTP method: GET, POST, PUT, DELETE, or PATCH. Defaults to GET if omitted."
                        },
                        headers = new
                        {
                            type = "object",
                            description = "Optional key-value pairs of HTTP request headers (e.g. { \"Authorization\": \"Bearer token\", \"Accept\": \"application/json\" })."
                        },
                        body = new
                        {
                            type = "string",
                            description = "Optional request body string (usually JSON) to send with POST, PUT, or PATCH requests."
                        }
                    },
                    required = new[] { "url" }
                }
            },
            Execute = async (argumentsJson) =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<HttpRequestArguments>(argumentsJson, s_jsonOptions);
                    if (args is null || string.IsNullOrWhiteSpace(args.Url))
                    {
                        return "{\"error\": \"Missing required parameter 'url' for http_request tool.\"}";
                    }

                    if (!Uri.TryCreate(args.Url.Trim(), UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return $"{{\"error\": \"Invalid URL schema. Only absolute HTTP and HTTPS URLs are supported: '{args.Url}'\"}}";
                    }

                    var methodString = (args.Method ?? "GET").Trim().ToUpperInvariant();
                    var httpMethod = new HttpMethod(methodString);

                    var client = httpClientFactory?.CreateClient("MaIN_HttpRequestTool") ?? new HttpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var request = new HttpRequestMessage(httpMethod, uri);

                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MaIN-Agent", "1.0"));

                    if (args.Headers is not null)
                    {
                        foreach (var (key, value) in args.Headers)
                        {
                            if (string.IsNullOrWhiteSpace(key)) continue;
                            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                            request.Headers.TryAddWithoutValidation(key, value);
                        }
                    }

                    if (!string.IsNullOrEmpty(args.Body) && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
                    {
                        var contentType = "application/json";
                        if (args.Headers is not null &&
                            args.Headers.FirstOrDefault(kvp => string.Equals(kvp.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value is { } ctHeader &&
                            !string.IsNullOrWhiteSpace(ctHeader))
                        {
                            contentType = ctHeader;
                        }

                        request.Content = new StringContent(args.Body, Encoding.UTF8, contentType);
                    }

                    using var response = await client.SendAsync(request, cts.Token);
                    var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

                    const int maxChars = 15_000;
                    var isTruncated = false;
                    if (responseBody.Length > maxChars)
                    {
                        responseBody = responseBody[..maxChars];
                        isTruncated = true;
                    }

                    var responseHeaders = new Dictionary<string, string>();
                    foreach (var header in response.Headers)
                    {
                        responseHeaders[header.Key] = string.Join(", ", header.Value);
                    }
                    foreach (var header in response.Content.Headers)
                    {
                        responseHeaders[header.Key] = string.Join(", ", header.Value);
                    }

                    var resultObject = new
                    {
                        status_code = (int)response.StatusCode,
                        success = response.IsSuccessStatusCode,
                        truncated = isTruncated,
                        headers = responseHeaders,
                        body = responseBody
                    };

                    return JsonSerializer.Serialize(resultObject, s_jsonOptions);
                }
                catch (TaskCanceledException)
                {
                    return "{\"error\": \"HTTP request timed out after 15 seconds.\"}";
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { error = $"HTTP request failed: {ex.Message}" }, s_jsonOptions);
                }
            }
        };
    }
}
