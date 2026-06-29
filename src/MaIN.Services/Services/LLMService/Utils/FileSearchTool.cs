using System.Text.Json;
using MaIN.Domain.Entities.Tools;
using MaIN.Services.Services.LLMService.Memory;

namespace MaIN.Services.Services.LLMService.Utils;

internal static class FileSearchTool
{
    public const string Name = "search_documents";

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ToolDefinition Create(IIngestedMemory memory, CancellationToken ct = default)
    {
        return new ToolDefinition
        {
            Function = new()
            {
                Name = Name,
                Description = """
                    Search the attached documents for information relevant to the user's question.
                    Call this tool whenever the answer may depend on the content of attached files.
                    You can rephrase the query to improve retrieval accuracy."
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Natural-language search query to run against the attached documents."
                        }
                    },
                    required = new[] { "query" }
                }
            },
            Execute = argsJson => ExecuteAsync(memory, argsJson, ct)
        };
    }

    private static Task<string> ExecuteAsync(IIngestedMemory memory, string argsJson, CancellationToken ct)
    {
        var query = ExtractQuery(argsJson);
        return memory.SearchAsync(query, ct);
    }

    private static string ExtractQuery(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.TryGetProperty("query", out var queryElement))
            {
                return queryElement.GetString() ?? argsJson;
            }
        }
        catch (JsonException)
        {
            // Raw string fallback — some local backends emit the query directly.
        }

        return argsJson;
    }
}
