using System.Net.Http.Json;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsBuiltInToolsTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task ChatCompletions_ExecutesBuiltInHttpRequestServerSide_WhenRequestedAsTool()
    {
        HttpHandler.ResponseBody = OpenAiResponse("API status is operational.");
        HttpHandler.EnqueueResponse(OpenAiToolCallResponse("http_request", """{"url":"https://api.example.com/status"}"""));
        HttpHandler.EnqueueResponse("{\"status\": \"operational\"}");
        HttpHandler.EnqueueResponse(OpenAiResponse("API status is operational."));

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "Check API status." } },
            tools = new object[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "http_request"
                    }
                }
            }
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];

        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());
        Assert.Equal("API status is operational.", choice.GetProperty("message").GetProperty("content").GetString());
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
