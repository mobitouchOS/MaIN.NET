using System.Net.Http.Json;
using System.Text.Json;
using MaIN.Core;
using MaIN.Core.Hub;
using MaIN.Domain.Configuration;
using MaIN.InferPage.Endpoints;
using MaIN.InferPage.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MaIN.InferPage.E2ETests;

/// <summary>
/// Real end-to-end coverage of the OpenAI-compatible endpoint against an actual local model
/// (qwen2.5-0.5b -- small and fast, already used elsewhere in this repo for the same reason,
/// see MaIN.Core.E2ETests/MinimalApiCarColorTests.cs). Auto-downloads the model on first run if
/// it isn't already present (~380MB from Hugging Face) -- expect the first run to be slow.
/// Excluded from CI via the standard `FullyQualifiedName!~E2ETests` filter; run manually:
///   dotnet test MaIN.InferPage.E2ETests/MaIN.InferPage.E2ETests.csproj
/// </summary>
public class OpenAiCompatEndpointE2ETests : IAsyncLifetime
{
    private const string ModelId = "qwen2.5-0.5b";
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string _modelsDir = null!;
    private string _dataDir = null!;

    public async Task InitializeAsync()
    {
        _modelsDir = Directory.GetCurrentDirectory();
        _dataDir = Path.Combine(Path.GetTempPath(), "main-inferpage-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        Environment.SetEnvironmentVariable("MaIN_ModelsPath", _modelsDir);

        Utils.BackendType = BackendType.Self;
        Utils.Model = ModelId;
        Utils.Path = _modelsDir;
        Utils.NeedsConfiguration = false;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"]
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MaIN:FileSystemSettings:Path"] = _dataDir
        });

        builder.Services.AddMaIN(builder.Configuration, settings =>
        {
            settings.ModelsPath = _modelsDir;
        });
        builder.Services.AddSingleton<AgentDefinitionService>();
        builder.Services.AddSingleton<AgentRunner>();

        _app = builder.Build();
        _app.Services.UseMaIN();
        _app.MapOpenAiCompatEndpoints();
        _app.Start();

        await AIHub.Model().EnsureDownloadedAsync(ModelId);

        OpenAiCompatEndpoints.InitializeModelCache();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }

    [Fact]
    public async Task ChatCompletions_ReturnsRealAssistantReply()
    {
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "Say the single word: hello" } },
            max_tokens = 20
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var content = body.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public async Task ChatCompletions_StreamsRealTokens()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                messages = new[] { new { role = "user", content = "Count from 1 to 3." } },
                stream = true,
                max_tokens = 30
            })
        };

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("data: ", body);
        Assert.EndsWith("data: [DONE]\n\n", body);
    }

    [Fact]
    public async Task Models_ListsTheRealActiveModel()
    {
        var response = await _client.GetAsync("/v1/models");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetString())
            .ToList();
        Assert.Contains(ModelId, ids);
    }
}
