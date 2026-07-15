using System.Net.Http.Json;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsBuiltInToolsTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task ChatCompletions_ExecutesBuiltInCalculatorServerSide_WhenRequestedAsTool()
    {
        HttpHandler.EnqueueResponse(OpenAiToolCallResponse("calculator", """{"expression":"20 + 22"}"""));
        HttpHandler.EnqueueResponse(OpenAiResponse("The exact answer is 42."));

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "Compute 20 + 22 exactly." } },
            tools = new object[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "calculator"
                    }
                }
            }
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];

        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());
        Assert.Equal("The exact answer is 42.", choice.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatCompletions_SupportsOpenAiHostedWebSearchPreviewType_WithoutFunctionDefinition()
    {
        HttpHandler.ResponseBody = OpenAiResponse("I have enabled web search capabilities for this query.");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "What is happening today?" } },
            tools = new object[]
            {
                new
                {
                    type = "web_search_preview"
                }
            }
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];

        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());
        Assert.Equal("I have enabled web search capabilities for this query.", choice.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatCompletions_SupportsOpenAiHostedWebSearchType_WithoutFunctionDefinition()
    {
        HttpHandler.ResponseBody = OpenAiResponse("Search results found for query.");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "Tell me latest news today." } },
            tools = new object[]
            {
                new
                {
                    type = "web_search"
                }
            }
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];

        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());
        Assert.Equal("Search results found for query.", choice.GetProperty("message").GetProperty("content").GetString());
    }
}
