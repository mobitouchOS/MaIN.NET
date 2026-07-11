using System.Text.Json;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;

namespace MaIN.InferPage.Endpoints;

public static class OpenAiInferenceParamsMapper
{
    public static IBackendInferenceParams Build(BackendType backend, ChatCompletionRequest request)
    {
        var temperature = request.Temperature.HasValue ? (float?)request.Temperature.Value : null;
        var topP = request.TopP.HasValue ? (float?)request.TopP.Value : null;
        var (nativeResponseFormat, grammar) = ResolveResponseFormat(backend, request.ResponseFormat);

        return backend switch
        {
            BackendType.Self => new LocalInferenceParams
            {
                Temperature = temperature ?? 0.8f,
                TopP = topP ?? 0.9f,
                MaxTokens = request.MaxTokens ?? -1,
                Grammar = grammar
            },
            BackendType.OpenAi => new OpenAiInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                ResponseFormat = nativeResponseFormat,
                Grammar = grammar
            },
            BackendType.DeepSeek => new DeepSeekInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                ResponseFormat = nativeResponseFormat,
                Grammar = grammar
            },
            BackendType.GroqCloud => new GroqCloudInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                ResponseFormat = nativeResponseFormat,
                Grammar = grammar
            },
            BackendType.Xai => new XaiInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                Grammar = grammar
            },
            BackendType.Gemini => new GeminiInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                StopSequences = request.Stop?.ToArray(),
                Grammar = grammar
            },
            BackendType.Anthropic => new AnthropicInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                Grammar = grammar
            },
            BackendType.Ollama => new OllamaInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                Grammar = grammar
            },
            BackendType.Vertex => new VertexInferenceParams
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = request.MaxTokens,
                StopSequences = request.Stop?.ToArray(),
                Grammar = grammar
            },
            _ => new LocalInferenceParams { Grammar = grammar }
        };
    }

    private static (string? NativeResponseFormat, Grammar? Grammar) ResolveResponseFormat(
        BackendType backend, ChatCompletionResponseFormat? responseFormat)
    {
        if (responseFormat is null || responseFormat.Type is "text")
        {
            return (null, null);
        }

        if (responseFormat.Type == "json_schema" && responseFormat.JsonSchema?.Schema is { } schema)
        {
            return (null, new Grammar(schema.GetRawText(), GrammarFormat.JSONSchema));
        }

        if (responseFormat.Type == "json_object")
        {
            var supportsNativeJsonObject = backend is BackendType.OpenAi or BackendType.DeepSeek or BackendType.GroqCloud;
            return supportsNativeJsonObject
                ? ("json_object", null)
                : (null, new Grammar("""{"type":"object"}""", GrammarFormat.JSONSchema));
        }

        return (null, null);
    }
}
