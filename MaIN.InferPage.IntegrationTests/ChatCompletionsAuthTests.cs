using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsAuthTests : InferPageEndpointTestBase, IDisposable
{
    public ChatCompletionsAuthTests()
    {
        Environment.SetEnvironmentVariable("MaIN__ApiKey", "test-secret");
    }

    void IDisposable.Dispose()
    {
        Environment.SetEnvironmentVariable("MaIN__ApiKey", null);
    }

    [Fact]
    public async Task Models_ReturnsUnauthorized_WhenNoBearerTokenProvided()
    {
        var response = await Client.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Models_ReturnsOk_WhenBearerTokenMatches()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-secret");

        var response = await Client.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChatCompletions_ReturnsUnauthorized_WhenBearerTokenWrong()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-secret");
        HttpHandler.ResponseBody = OpenAiResponse("should not be reached");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "hi" } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
