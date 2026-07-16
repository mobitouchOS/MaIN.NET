namespace MaIN.Domain.Entities.Tools;

public class ToolInvocation
{
    public required string ToolName { get; set; }
    public string Arguments { get; set; } = null!;
    public bool Done { get; set; } = false;
    public bool IsClientSide { get; set; } = false;
}