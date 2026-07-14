using System.Data;
using System.Text.Json;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class CalculatorTool
{
    public const string Name = "calculator";
    public const string AliasName = "math_eval";

    public static ToolDefinition Create(string toolName = Name)
    {
        return new ToolDefinition
        {
            Type = "function",
            IsClientSide = false,
            Function = new()
            {
                Name = toolName,
                Description = """
                    Evaluate numerical mathematical and arithmetic expressions safely and accurately.
                    Call this tool whenever you need to compute exact numbers, percentages, or equations rather than guessing.
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        expression = new
                        {
                            type = "string",
                            description = "Mathematical arithmetic expression to evaluate (e.g., '1234 * 56.7 + (89 - 12)')."
                        }
                    },
                    required = new[] { "expression" }
                }
            },
            Execute = argsJson =>
            {
                var expr = ExtractExpression(argsJson);
                if (string.IsNullOrWhiteSpace(expr))
                {
                    return Task.FromResult("Error: Empty mathematical expression provided.");
                }

                try
                {
                    using var table = new DataTable();
                    var result = table.Compute(expr, null);
                    return Task.FromResult($"Result of '{expr}' = {result}");
                }
                catch (Exception ex)
                {
                    return Task.FromResult($"Error evaluating expression '{expr}': {ex.Message}");
                }
            }
        };
    }

    private static string ExtractExpression(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("expression", out var exprElement))
            {
                return exprElement.GetString() ?? argsJson;
            }
        }
        catch { }
        return argsJson;
    }
}
