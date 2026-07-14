using System.Text.Json;

namespace MaIN.InferPage.Endpoints;

public sealed class ChatCompletionRequest
{
    public string? Model { get; set; }
    public List<ChatCompletionRequestMessage> Messages { get; set; } = [];
    public bool? Stream { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public List<string>? Stop { get; set; }
    public List<ChatCompletionTool>? Tools { get; set; }
    public string? ToolChoice { get; set; }
    public ChatCompletionResponseFormat? ResponseFormat { get; set; }
}

public sealed class ChatCompletionRequestMessage
{
    public string Role { get; set; } = string.Empty;
    public JsonElement? Content { get; set; }
    public List<ChatCompletionToolCallDto>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
}

public sealed class ChatCompletionTool
{
    public string Type { get; set; } = "function";
    public ChatCompletionFunctionDefinition? Function { get; set; }
}

public sealed class ChatCompletionFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement? Parameters { get; set; }
}

public sealed class ChatCompletionToolCallDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public required ChatCompletionFunctionCallDto Function { get; set; }
}

public sealed class ChatCompletionFunctionCallDto
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}

public sealed class ChatCompletionResponseFormat
{
    public string Type { get; set; } = "text";
    public ChatCompletionJsonSchema? JsonSchema { get; set; }
}

public sealed class ChatCompletionJsonSchema
{
    public string? Name { get; set; }
    public JsonElement? Schema { get; set; }
}

public sealed class ChatCompletionResponse
{
    public string Id { get; set; } = $"chatcmpl-{Guid.NewGuid():N}";
    public string Object { get; set; } = "chat.completion";
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<ChatCompletionChoice> Choices { get; set; } = [];
    public ChatCompletionUsage Usage { get; set; } = new();
}

public sealed class ChatCompletionChoice
{
    public int Index { get; set; }
    public ChatCompletionResponseMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public sealed class ChatCompletionResponseMessage
{
    public string Role { get; set; } = "assistant";
    public string? Content { get; set; }
    public List<ChatCompletionToolCallDto>? ToolCalls { get; set; }
}

public sealed class ChatCompletionUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public sealed class ChatCompletionChunk
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "chat.completion.chunk";
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<ChatCompletionChunkChoice> Choices { get; set; } = [];
}

public sealed class ChatCompletionChunkChoice
{
    public int Index { get; set; }
    public ChatCompletionChunkDelta Delta { get; set; } = new();
    public string? FinishReason { get; set; }
}

public sealed class ChatCompletionChunkDelta
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    public List<ChatCompletionToolCallDto>? ToolCalls { get; set; }
}

public sealed class ModelListResponse
{
    public string Object { get; set; } = "list";
    public List<ModelData> Data { get; set; } = [];
}

public sealed class ModelData
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "model";
    public long Created { get; set; }
    public string OwnedBy { get; set; } = "main-inferpage";
}

public sealed class OpenAiErrorResponse
{
    public OpenAiError Error { get; set; } = new();
}

public sealed class OpenAiError
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "invalid_request_error";
    public string? Code { get; set; }
}
