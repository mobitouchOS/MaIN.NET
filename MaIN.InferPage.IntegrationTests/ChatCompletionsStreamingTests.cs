namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsStreamingTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task ChatCompletions_StreamsServerSentEventChunks()
    {
        HttpHandler.ResponseBody = OpenAiStreamResponse("Hi");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                messages = new[] { new { role = "user", content = "Say hi" } },
                stream = true
            })
        };

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("data: ", body);
        Assert.Contains("\"content\":\"Hi\"", body);
        Assert.Contains("\"finish_reason\":\"stop\"", body);
        Assert.EndsWith("data: [DONE]\n\n", body);
    }
}
