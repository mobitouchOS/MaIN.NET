namespace MaIN.Domain.Entities.Tools;

public class ToolsConfiguration
{
    public required List<ToolDefinition> Tools { get; set; }
    public string? ToolChoice { get; set; }
    public int? MaxIterations { get; set; }

    private static bool IsWebSearchIdentifier(string? identifier) =>
        string.Equals(identifier, "web_search", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(identifier, "web_search_preview", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(identifier, "search_web", StringComparison.OrdinalIgnoreCase);

    public Func<string, Task<string>>? GetExecutor(string functionName)
    {
        return GetDefinition(functionName)?.Execute;
    }

    public ToolDefinition? GetDefinition(string functionName)
    {
        return Tools.FirstOrDefault(t =>
            string.Equals(t.Function?.Name, functionName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Type, functionName, StringComparison.OrdinalIgnoreCase) ||
            (IsWebSearchIdentifier(functionName) && (IsWebSearchIdentifier(t.Function?.Name) || IsWebSearchIdentifier(t.Type))));
    }
}
