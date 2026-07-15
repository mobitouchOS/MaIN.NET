using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaIN.InferPage.Endpoints;

public sealed class CreateResponseRequest
{
    public string? Model { get; set; }
    public JsonElement? Input { get; set; }
    public string? Instructions { get; set; }
    public bool? Stream { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
    public List<ChatCompletionTool>? Tools { get; set; }
    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }
    [JsonPropertyName("response_format")]
    public ChatCompletionResponseFormat? ResponseFormat { get; set; }

    [JsonIgnore]
    public int? ResolvedMaxTokens => MaxOutputTokens ?? MaxTokens;
}

public sealed class ResponsesResponse
{
    public string Id { get; set; } = $"resp_{Guid.NewGuid():N}";
    public string Object { get; set; } = "response";
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
    public List<object> Output { get; set; } = [];
    public ChatCompletionUsage Usage { get; set; } = new();
}

public sealed class ResponseOutputItemMessage
{
    public string Id { get; set; } = $"msg_{Guid.NewGuid():N}";
    public string Type { get; set; } = "message";
    public string Role { get; set; } = "assistant";
    public List<ResponseOutputContentText> Content { get; set; } = [];
}

public sealed class ResponseOutputContentText
{
    public string Type { get; set; } = "output_text";
    public string Text { get; set; } = string.Empty;
}

public sealed class ResponseOutputItemFunctionCall
{
    public string Id { get; set; } = $"call_{Guid.NewGuid():N}";
    public string Type { get; set; } = "function_call";
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}
