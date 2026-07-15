using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ResponsesEndpointTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task Responses_ReturnsAssistantMessage_ForNonStreamingStringInput()
    {
        HttpHandler.ResponseBody = OpenAiResponse("Hello from Responses API!");

        var response = await Client.PostAsJsonAsync("/v1/responses", new
        {
            model = ModelId,
            input = "Say hello",
            instructions = "You are a helpful assistant."
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("response", body.GetProperty("object").GetString());
        Assert.Equal("completed", body.GetProperty("status").GetString());
        
        var output = body.GetProperty("output");
        Assert.Equal(1, output.GetArrayLength());
        
        var firstItem = output[0];
        Assert.Equal("message", firstItem.GetProperty("type").GetString());
        Assert.Equal("assistant", firstItem.GetProperty("role").GetString());
        Assert.Equal("Hello from Responses API!", firstItem.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Responses_ReturnsAssistantMessage_ForNonStreamingArrayInput()
    {
        HttpHandler.ResponseBody = OpenAiResponse("Array input response.");

        var response = await Client.PostAsJsonAsync("/v1/responses", new
        {
            model = ModelId,
            input = new object[]
            {
                new { role = "user", content = "First question" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var output = body.GetProperty("output");
        Assert.Equal("Array input response.", output[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Responses_ExecutesBuiltInWebSearch_WhenRequested()
    {
        HttpHandler.ResponseBody = OpenAiResponse("Web search executed successfully.");

        var response = await Client.PostAsJsonAsync("/v1/responses", new
        {
            input = "What is the news?",
            tools = new object[]
            {
                new { type = "web_search" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var output = body.GetProperty("output");
        Assert.Equal("Web search executed successfully.", output[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Responses_StreamsServerSentEventChunks()
    {
        HttpHandler.ResponseBody = OpenAiStreamResponse("Streaming output from Responses");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                input = "Stream hello",
                stream = true
            })
        };

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: response.created", body);
        Assert.Contains("event: response.output_item.added", body);
        Assert.Contains("event: response.content_part.added", body);
        Assert.Contains("event: response.output_text.delta", body);
        Assert.Contains("event: response.output_text.done", body);
        Assert.Contains("event: response.output_item.done", body);
        Assert.Contains("event: response.done", body);
        Assert.Contains("Streaming output from Responses", body);
    }

    [Fact]
    public async Task Responses_ReturnsModelNotFound_WhenRequestedModelDoesNotMatch()
    {
        var response = await Client.PostAsJsonAsync("/v1/responses", new
        {
            model = "non-existent-model",
            input = "hi"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("model_not_found", body.GetProperty("error").GetProperty("code").GetString());
    }
}
