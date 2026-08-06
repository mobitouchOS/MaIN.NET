using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MaIN.Core.Hub;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class AgentApiTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task Models_ListsSavedAgent_WithAgentPrefixedId()
    {
        var executor = await AIHub.Agent()
            .WithModel(ModelId)
            .WithName("ApiListedAgent")
            .WithInitialPrompt("You are a test agent.")
            .CreateAsync();
        var agentId = executor.GetAgentId();

        var response = await Client.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetString())
            .ToList();
        Assert.Contains($"agent:{agentId}", ids);
    }

    [Fact]
    public async Task ChatCompletions_ReturnsAssistantMessage_ForAgent()
    {
        var executor = await AIHub.Agent()
            .WithModel(ModelId)
            .WithName("ApiChatAgent")
            .WithInitialPrompt("You are a test agent.")
            .CreateAsync();
        var agentId = executor.GetAgentId();

        HttpHandler.ResponseBody = OpenAiResponse("Hello from agent!");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = $"agent:{agentId}",
            messages = new[] { new { role = "user", content = "Say hi" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];
        Assert.Equal("Hello from agent!", choice.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatCompletions_ForAgent_IsStatelessAcrossCalls()
    {
        var executor = await AIHub.Agent()
            .WithModel(ModelId)
            .WithName("ApiStatelessAgent")
            .WithInitialPrompt("You are a test agent.")
            .CreateAsync();
        var agentId = executor.GetAgentId();

        HttpHandler.ResponseBody = OpenAiResponse("First reply");
        var firstResponse = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = $"agent:{agentId}",
            messages = new[] { new { role = "user", content = "First turn message" } }
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        HttpHandler.ResponseBody = OpenAiResponse("Second reply");
        var secondResponse = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = $"agent:{agentId}",
            messages = new[] { new { role = "user", content = "Second turn message" } }
        });
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var ctx = await AIHub.Agent().FromExisting(agentId);
        var chat = await ctx.GetChat();
        var contents = chat.Messages.Select(m => m.Content).ToList();

        Assert.Contains(contents, c => c.Contains("Second turn message"));
        Assert.DoesNotContain(contents, c => c.Contains("First turn message"));
    }

    [Fact]
    public async Task ChatCompletions_ReturnsNotFound_ForUnknownAgent()
    {
        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "agent:does-not-exist",
            messages = new[] { new { role = "user", content = "hi" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("model_not_found", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChatCompletions_ReturnsNotFound_ForUnknownAgent_WhenStreaming()
    {
        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "agent:does-not-exist",
            stream = true,
            messages = new[] { new { role = "user", content = "hi" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("model_not_found", body.GetProperty("error").GetProperty("code").GetString());
    }
}
