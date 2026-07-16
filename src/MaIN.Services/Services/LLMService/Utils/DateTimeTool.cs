using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class DateTimeTool
{
    public const string Name = "get_current_datetime";
    public const string AliasName = "datetime";

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
                    Get the current exact date, time, day of the week, and UTC timezone.
                    Call this tool whenever you need to know today's date, the current time, or when comparing timestamps against current real-world time.
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            Execute = _ =>
            {
                var now = DateTimeOffset.UtcNow;
                return Task.FromResult($"Current exact time: {now:yyyy-MM-dd HH:mm:ss 'UTC'} ({now:dddd})");
            }
        };
    }
}
