using System.Text.Json;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Services.Constants;

namespace MaIN.InferPage.Endpoints;

public static class OpenAiMessageMapper
{
    public static string? ExtractText(JsonElement? content)
    {
        if (content is null || content.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var element = content.Value;
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var part in element.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString() == "text" &&
                    part.TryGetProperty("text", out var textProp))
                {
                    parts.Add(textProp.GetString() ?? string.Empty);
                }
            }

            return string.Concat(parts);
        }

        return element.GetRawText();
    }

    public static (string? SystemPrompt, List<Message> Messages) ToMainMessages(
        IReadOnlyList<ChatCompletionRequestMessage> incoming)
    {
        string? systemPrompt = null;
        var messages = new List<Message>();

        foreach (var m in incoming)
        {
            var role = m.Role?.ToLowerInvariant() ?? string.Empty;

            if (role == "system")
            {
                var text = ExtractText(m.Content) ?? string.Empty;
                systemPrompt = systemPrompt is null ? text : $"{systemPrompt}\n{text}";
                continue;
            }

            if (role == "tool")
            {
                var toolMessage = new Message
                {
                    Role = ServiceConstants.Roles.Tool,
                    Content = ExtractText(m.Content) ?? string.Empty,
                    Type = MessageType.NotSet,
                    Tool = true,
                    Time = DateTime.Now
                };

                if (!string.IsNullOrEmpty(m.ToolCallId))
                {
                    toolMessage.Properties[ServiceConstants.Properties.ToolCallIdProperty] = m.ToolCallId;
                }

                if (!string.IsNullOrEmpty(m.Name))
                {
                    toolMessage.Properties[ServiceConstants.Properties.ToolNameProperty] = m.Name;
                }

                messages.Add(toolMessage);
                continue;
            }

            var mappedRole = role == "assistant" ? "Assistant" : "User";
            var message = new Message
            {
                Role = mappedRole,
                Content = ExtractText(m.Content) ?? string.Empty,
                Type = MessageType.NotSet,
                Time = DateTime.Now
            };

            if (m.ToolCalls is { Count: > 0 })
            {
                var toolCalls = m.ToolCalls.Select(tc => new ToolCall
                {
                    Id = tc.Id,
                    Type = tc.Type,
                    Function = new FunctionCall { Name = tc.Function.Name, Arguments = tc.Function.Arguments }
                }).ToList();
                message.Properties[ServiceConstants.Properties.ToolCallsProperty] = JsonSerializer.Serialize(toolCalls);
            }

            messages.Add(message);
        }

        return (systemPrompt, messages);
    }

    public static List<ChatCompletionToolCallDto> ToToolCallDtos(IEnumerable<ToolInvocation> invocations)
    {
        return invocations.Select(inv => new ChatCompletionToolCallDto
        {
            Id = $"call_{Guid.NewGuid():N}",
            Type = "function",
            Function = new ChatCompletionFunctionCallDto { Name = inv.ToolName, Arguments = inv.Arguments }
        }).ToList();
    }
}
