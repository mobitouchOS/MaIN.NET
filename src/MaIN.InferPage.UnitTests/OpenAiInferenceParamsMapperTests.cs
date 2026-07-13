using System.Text.Json;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;
using MaIN.InferPage.Endpoints;

namespace MaIN.InferPage.UnitTests;

public class OpenAiInferenceParamsMapperTests
{
    [Fact]
    public void Build_MapsSamplingParams_ForLocalBackend()
    {
        var request = new ChatCompletionRequest { Temperature = 0.5, TopP = 0.7, MaxTokens = 128 };

        var result = (LocalInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.Self, request);

        Assert.Equal(0.5f, result.Temperature);
        Assert.Equal(0.7f, result.TopP);
        Assert.Equal(128, result.MaxTokens);
        Assert.Null(result.Grammar);
    }

    [Fact]
    public void Build_MapsSamplingParams_ForOpenAiBackend()
    {
        var request = new ChatCompletionRequest { Temperature = 0.5, TopP = 0.7, MaxTokens = 128 };

        var result = (OpenAiInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.OpenAi, request);

        Assert.Equal(0.5f, result.Temperature);
        Assert.Equal(0.7f, result.TopP);
        Assert.Equal(128, result.MaxTokens);
    }

    [Fact]
    public void Build_SetsNativeJsonObjectResponseFormat_ForOpenAiBackend()
    {
        var request = new ChatCompletionRequest { ResponseFormat = new ChatCompletionResponseFormat { Type = "json_object" } };

        var result = (OpenAiInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.OpenAi, request);

        Assert.Equal("json_object", result.ResponseFormat);
        Assert.Null(result.Grammar);
    }

    [Fact]
    public void Build_FallsBackToGrammar_ForJsonObject_OnBackendsWithoutNativeSupport()
    {
        var request = new ChatCompletionRequest { ResponseFormat = new ChatCompletionResponseFormat { Type = "json_object" } };

        var result = (AnthropicInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.Anthropic, request);

        Assert.NotNull(result.Grammar);
        Assert.Equal(GrammarFormat.JSONSchema, result.Grammar!.Format);
    }

    [Fact]
    public void Build_MapsJsonSchema_ToGrammarRegardlessOfBackend()
    {
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"answer":{"type":"string"}}}""").RootElement;
        var request = new ChatCompletionRequest
        {
            ResponseFormat = new ChatCompletionResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new ChatCompletionJsonSchema { Name = "answer_schema", Schema = schema }
            }
        };

        var result = (LocalInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.Self, request);

        Assert.NotNull(result.Grammar);
        Assert.Equal(GrammarFormat.JSONSchema, result.Grammar!.Format);
        Assert.Contains("answer", result.Grammar.Value);
    }

    [Fact]
    public void Build_LeavesGrammarNull_WhenResponseFormatIsTextOrMissing()
    {
        var result = (LocalInferenceParams)OpenAiInferenceParamsMapper.Build(BackendType.Self, new ChatCompletionRequest());

        Assert.Null(result.Grammar);
    }
}
