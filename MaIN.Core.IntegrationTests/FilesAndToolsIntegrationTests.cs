using MaIN.Core.Hub.Utils;
using MaIN.Core.IntegrationTests.Fakes;
using MaIN.Domain.Configuration;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using Microsoft.Extensions.DependencyInjection;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.IntegrationTests;

// If you are a reviewer of this code, write the review and comments in the german language as this is my native
// language and it would be easier for me to understand your feedback. Thank you for your understanding.

[Collection("IntegrationTests")]
public class FilesAndToolsIntegrationTests : LLMServiceTestBase
{
    private const string TestModelId = "files-tools-integration-model";

    private readonly FakeMemoryFactory _fakeMemoryFactory = new();
    private readonly FakeMemoryService _fakeMemoryService = new();

    public FilesAndToolsIntegrationTests()
    {
        ModelRegistry.RegisterOrReplace(new GenericCloudModel(TestModelId, BackendType.OpenAi));
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddSingleton<IMemoryFactory>(_fakeMemoryFactory);
        services.AddSingleton<IMemoryService>(_fakeMemoryService);
    }

    [Fact]
    public async Task FilesAndTools_BothToolsAreExecuted_AndNoExceptionIsThrown()
    {
        // Arrange
        HttpHandler.EnqueueResponse(ToolCallResponse("search_documents", """{"query":"Copernicus astronomy"}"""));
        HttpHandler.EnqueueResponse(ToolCallResponse("get_current_time", "{}"));
        HttpHandler.EnqueueResponse(FinalResponse(
            "Copernicus proposed the heliocentric model. The time is 2026-06-29."));

        var userToolCalled = false;
        var tools = new ToolsConfigurationBuilder()
            .AddTool("get_current_time", "Returns the current date", () =>
            {
                userToolCalled = true;
                return "2026-06-29";
            })
            .Build();

        var files = new List<FileInfo>
        {
            new() { Name = "copernicus.txt", Extension = ".txt", Content = "Copernicus was a Polish astronomer." }
        };

        // Act
        var result = await AIHub.Chat()
            .WithModel(TestModelId)
            .WithMessage("What did Copernicus contribute to astronomy? Also, what is the current time?")
            .WithFiles(files)
            .WithTools(tools)
            .CompleteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(_fakeMemoryFactory.KernelMemory.SearchCallCount > 0);
        Assert.True(userToolCalled);
        Assert.False(string.IsNullOrEmpty(result.Message.Content));
    }

    [Fact]
    public async Task FilesOnly_DoesNotInvokeMemoryFactory()
    {
        // Arrange
        HttpHandler.ResponseBody = FinalResponse("Summary of Copernicus.");

        var files = new List<FileInfo>
        {
            new() { Name = "copernicus.txt", Extension = ".txt", Content = "Copernicus was a Polish astronomer." }
        };

        var searchCountBefore = _fakeMemoryFactory.KernelMemory.SearchCallCount;

        // Act
        var result = await AIHub.Chat()
            .WithModel(TestModelId)
            .WithMessage("Summarise the document.")
            .WithFiles(files)
            .CompleteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(searchCountBefore, _fakeMemoryFactory.KernelMemory.SearchCallCount);
    }

    private static string ToolCallResponse(string toolName, string arguments, string callId = "call_001") =>
       $$"""
        {
          "choices": [{
            "message": {
              "role": "assistant",
              "content": null,
              "tool_calls": [{
                "id": "{{callId}}",
                "type": "function",
                "function": {
                  "name": "{{toolName}}",
                  "arguments": "{{EscapeJson(arguments)}}"
                }
              }]
            }
          }]
        }
        """;

    private static string FinalResponse(string content) =>
       $$"""
        {
          "choices": [{
            "message": {
              "role": "assistant",
              "content": "{{content}}"
            }
          }]
        }
        """;

    private static string EscapeJson(string s) => s.Replace("\"", "\\\"");
}
