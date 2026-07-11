using System.Net.Http.Json;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests;

[Collection("InferPageEndpointTests")]
public class ChatCompletionsToolCallingTests : InferPageEndpointTestBase
{
    [Fact]
    public async Task ChatCompletions_ReturnsToolCalls_WhenModelRequestsATool()
    {
        HttpHandler.ResponseBody = OpenAiToolCallResponse("get_weather", """{"city":"Paris"}""");

        var response = await Client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "What's the weather in Paris?" } },
            tools = new object[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_weather",
                        description = "Get the current weather for a city",
                        parameters = new { type = "object", properties = new { city = new { type = "string" } } }
                    }
                }
            }
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = body.GetProperty("choices")[0];

        Assert.Equal("tool_calls", choice.GetProperty("finish_reason").GetString());
        var toolCall = choice.GetProperty("message").GetProperty("tool_calls")[0];
        Assert.Equal("get_weather", toolCall.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("""{"city":"Paris"}""", toolCall.GetProperty("function").GetProperty("arguments").GetString());
    }
}
