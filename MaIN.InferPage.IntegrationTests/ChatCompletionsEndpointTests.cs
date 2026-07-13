using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsEndpointTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task ChatCompletions_ReturnsAssistantMessage_ForNonStreamingRequest()
    {
        HttpHandler.ResponseBody = OpenAiResponse("Hello there!");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "Say hi" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];
        Assert.Equal("Hello there!", choice.GetProperty("message").GetProperty("content").GetString());
        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());
    }

    [Fact]
    public async Task ChatCompletions_ReturnsModelNotFound_WhenRequestedModelDoesNotMatch()
    {
        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "some-other-model",
            messages = new[] { new { role = "user", content = "hi" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("model_not_found", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChatCompletions_ReturnsBadRequest_WhenMessagesEmpty()
    {
        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new { messages = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Models_ListsTheActiveModel()
    {
        var response = await Client.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("list", body.GetProperty("object").GetString());
        Assert.Equal(ModelId, body.GetProperty("data")[0].GetProperty("id").GetString());
    }
}
