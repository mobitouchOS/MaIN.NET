using System.Text.Json;
using MaIN.Core;
using MaIN.Domain.Configuration;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;
using MaIN.InferPage.Endpoints;
using MaIN.InferPage.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MaIN.InferPage.IntegrationTests;

[CollectionDefinition("InferPageEndpointTests")]
public class InferPageEndpointTestCollection
{
}

public abstract class InferPageEndpointTestBase : IAsyncDisposable
{
    protected const string ModelId = "gpt-4o-mini";

    protected readonly FakeHttpClientFactory FakeClientFactory = new();
    protected FakeHttpMessageHandler HttpHandler => FakeClientFactory.Handler;
    protected readonly HttpClient Client;

    private readonly WebApplication _app;

    protected InferPageEndpointTestBase()
    {
        ModelRegistry.RegisterOrReplace(new GenericCloudModel(ModelId, BackendType.OpenAi));
        Utils.BackendType = BackendType.OpenAi;
        Utils.Model = ModelId;
        Utils.NeedsConfiguration = false;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"]
        });

        builder.Services.AddMaIN(builder.Configuration);
        builder.Services.AddSingleton<IHttpClientFactory>(FakeClientFactory);
        builder.Services.AddSingleton(new MaINSettings { OpenAiKey = "test-openai-key" });

        _app = builder.Build();
        _app.Services.UseMaIN();
        _app.MapOpenAiCompatEndpoints();
        _app.Start();

        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    protected static string OpenAiResponse(string content, string model = ModelId) =>
        $$"""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "{{content}}"
              }
            }
          ],
          "model": "{{model}}"
        }
        """;

    protected static string OpenAiStreamResponse(string content) =>
        $$$"""
        data: {"choices":[{"delta":{"content":"{{{content}}}"}}]}
        data: [DONE]

        """;

    protected static string OpenAiToolCallResponse(string toolName, string arguments, string callId = "call_abc123") =>
        // Real OpenAI responses carry `function.arguments` as a JSON-encoded STRING (not a nested
        // object) -- JsonSerializer.Serialize(arguments) here escapes the caller's raw JSON text
        // (e.g. {"city":"Paris"}) into that string form, matching what OpenAiCompatibleService's
        // tool-call deserialization (a plain `string Arguments` property) expects.
        $$"""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": null,
                "tool_calls": [
                  {
                    "id": "{{callId}}",
                    "type": "function",
                    "function": { "name": "{{toolName}}", "arguments": {{JsonSerializer.Serialize(arguments)}} }
                  }
                ]
              },
              "finish_reason": "tool_calls"
            }
          ],
          "model": "{{ModelId}}"
        }
        """;

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
