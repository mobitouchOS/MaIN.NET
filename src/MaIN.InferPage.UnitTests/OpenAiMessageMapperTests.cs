using System.Text.Json;
using MaIN.Domain.Entities.Tools;
using MaIN.InferPage.Endpoints;
using MaIN.Services.Constants;

namespace MaIN.InferPage.UnitTests;

public class OpenAiMessageMapperTests
{
    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ExtractText_ReturnsPlainString_WhenContentIsAString()
    {
        var content = ParseElement("\"hello world\"");

        var result = OpenAiMessageMapper.ExtractText(content);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ExtractText_ConcatenatesTextParts_WhenContentIsAnArray()
    {
        var content = ParseElement("""[{"type":"text","text":"foo"},{"type":"text","text":"bar"}]""");

        var result = OpenAiMessageMapper.ExtractText(content);

        Assert.Equal("foobar", result);
    }

    [Fact]
    public void ExtractText_ReturnsNull_WhenContentIsNull()
    {
        var result = OpenAiMessageMapper.ExtractText(null);

        Assert.Null(result);
    }

    [Fact]
    public void ToMainMessages_ExtractsSystemPromptSeparately()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new() { Role = "system", Content = ParseElement("\"be helpful\"") },
            new() { Role = "user", Content = ParseElement("\"hi\"") }
        };

        var (systemPrompt, messages) = OpenAiMessageMapper.ToMainMessages(incoming);

        Assert.Equal("be helpful", systemPrompt);
        Assert.Single(messages);
        Assert.Equal("User", messages[0].Role);
        Assert.Equal("hi", messages[0].Content);
    }

    [Fact]
    public void ToMainMessages_MapsAssistantRoleToPascalCase()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new() { Role = "assistant", Content = ParseElement("\"ok\"") }
        };

        var (_, messages) = OpenAiMessageMapper.ToMainMessages(incoming);

        Assert.Equal("Assistant", messages[0].Role);
    }

    [Fact]
    public void ToMainMessages_MapsToolResultMessage()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new() { Role = "tool", Content = ParseElement("\"42 degrees\""), ToolCallId = "call_1", Name = "get_weather" }
        };

        var (_, messages) = OpenAiMessageMapper.ToMainMessages(incoming);

        Assert.Single(messages);
        Assert.True(messages[0].Tool);
        Assert.Equal("42 degrees", messages[0].Content);
        Assert.Equal("call_1", messages[0].Properties[ServiceConstants.Properties.ToolCallIdProperty]);
        Assert.Equal("get_weather", messages[0].Properties[ServiceConstants.Properties.ToolNameProperty]);
    }

    [Fact]
    public void ToMainMessages_MapsAssistantToolCallsIntoProperties()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new()
            {
                Role = "assistant",
                Content = null,
                ToolCalls =
                [
                    new ChatCompletionToolCallDto
                    {
                        Id = "call_1",
                        Function = new ChatCompletionFunctionCallDto { Name = "get_weather", Arguments = "{\"city\":\"NYC\"}" }
                    }
                ]
            }
        };

        var (_, messages) = OpenAiMessageMapper.ToMainMessages(incoming);

        Assert.True(messages[0].Properties.ContainsKey(ServiceConstants.Properties.ToolCallsProperty));
        var toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(messages[0].Properties[ServiceConstants.Properties.ToolCallsProperty]);
        Assert.Single(toolCalls!);
        Assert.Equal("get_weather", toolCalls![0].Function.Name);
    }

    [Fact]
    public void ToToolCallDtos_MintsUniqueIdsAndCopiesNameAndArguments()
    {
        var invocations = new List<ToolInvocation>
        {
            new() { ToolName = "get_weather", Arguments = "{\"city\":\"NYC\"}", Done = false }
        };

        var dtos = OpenAiMessageMapper.ToToolCallDtos(invocations);

        Assert.Single(dtos);
        Assert.NotEmpty(dtos[0].Id);
        Assert.Equal("get_weather", dtos[0].Function.Name);
        Assert.Equal("{\"city\":\"NYC\"}", dtos[0].Function.Arguments);
    }

    [Fact]
    public void ToMainMessages_HandlesNullRoleGracefully()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new() { Role = null! }
        };

        var exception = Record.Exception(() => OpenAiMessageMapper.ToMainMessages(incoming));

        Assert.Null(exception);
    }

    [Fact]
    public void ToMainMessages_MapsAssistantToolCallsAndToolResultMessages()
    {
        var incoming = new List<ChatCompletionRequestMessage>
        {
            new()
            {
                Role = "assistant",
                Content = JsonSerializer.SerializeToElement("Checking weather..."),
                ToolCalls = new List<ChatCompletionToolCallDto>
                {
                    new()
                    {
                        Id = "call_abc",
                        Type = "function",
                        Function = new ChatCompletionFunctionCallDto { Name = "get_weather", Arguments = "{\"city\":\"Paris\"}" }
                    }
                }
            },
            new()
            {
                Role = "tool",
                ToolCallId = "call_abc",
                Name = "get_weather",
                Content = JsonSerializer.SerializeToElement("22C and sunny")
            }
        };

        var (_, messages) = OpenAiMessageMapper.ToMainMessages(incoming);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Assistant", messages[0].Role);
        Assert.True(messages[0].Properties.ContainsKey(ServiceConstants.Properties.ToolCallsProperty));

        Assert.Equal(ServiceConstants.Roles.Tool, messages[1].Role);
        Assert.True(messages[1].Tool);
        Assert.Equal("call_abc", messages[1].Properties[ServiceConstants.Properties.ToolCallIdProperty]);
        Assert.Equal("get_weather", messages[1].Properties[ServiceConstants.Properties.ToolNameProperty]);
    }
}
