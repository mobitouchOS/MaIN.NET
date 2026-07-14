namespace MaIN.Domain.Entities.Tools;

public class ToolsConfiguration
{
    public required List<ToolDefinition> Tools { get; set; }
    public string? ToolChoice { get; set; }
    public int? MaxIterations { get; set; }

    public Func<string, Task<string>>? GetExecutor(string functionName)
    {
        return Tools.FirstOrDefault(t => t.Function?.Name == functionName)?.Execute;
    }

    public ToolDefinition? GetDefinition(string functionName)
    {
        return Tools.FirstOrDefault(t => t.Function?.Name == functionName);
    }
}
