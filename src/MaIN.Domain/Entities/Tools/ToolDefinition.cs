using System.Text.Json.Serialization;

namespace MaIN.Domain.Entities.Tools;

public class ToolDefinition
{
    public string Type { get; set; } = "function";
    public FunctionDefinition? Function { get; set; } 
    
    [JsonIgnore]
    public Func<string, Task<string>>? Execute { get; set; }

    [JsonIgnore]
    public bool IsClientSide { get; set; }
}