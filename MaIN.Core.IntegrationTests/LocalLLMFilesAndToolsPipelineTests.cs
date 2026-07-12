using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.Models;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.IntegrationTests;

[Collection("IntegrationTests")]
public class LocalLLMFilesAndToolsPipelineTests : PipelineTestBase
{
    private const string LocalModelId = "local-files-tools-test-model";

    public LocalLLMFilesAndToolsPipelineTests()
    {
        ModelRegistry.RegisterOrReplace(new GenericLocalModel(LocalModelId));
    }

    [Fact]
    public async Task FilesAndTools_Local_ChatReachesServiceWithFilesAndToolsPreserved()
    {
        // Arrange
        Chat? captured = null;
        FakeFactory.Service.Handler = chat =>
        {
            captured = chat;
            return new ChatResult
            {
                Model = chat.ModelId ?? LocalModelId,
                Done = true,
                CreatedAt = DateTime.UtcNow,
                Message = new Message { Role = "assistant", Content = "ok", Type = MessageType.LocalLLM }
            };
        };
        var tools = new ToolsConfiguration
        {
            Tools =
            [
                new ToolDefinition
                {
                    Function = new FunctionDefinition
                    {
                        Name = "get_current_time",
                        Description = "Returns the current date",
                        Parameters = new { type = "object", properties = new { } }
                    },
                    Execute = _ =>
                    {
                        return Task.FromResult("2026-06-29");
                    }
                }
            ]
        };

        var files = new List<FileInfo>
        {
            new() { Name = "doc.txt", Extension = ".txt", Content = "Some document content." }
        };

        // Act
        var result = await AIHub.Chat()
            .WithModel(LocalModelId)
            .WithMessage("Summarise the doc and tell me the time.")
            .WithFiles(files)
            .WithTools(tools)
            .CompleteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(captured);
        Assert.IsType<LocalInferenceParams>(captured.BackendParams);
        var lastMsg = captured.Messages.Last(m => m.Role == "User");
        Assert.NotEmpty(lastMsg.Files ?? []);
        Assert.NotNull(captured.ToolsConfiguration);
        Assert.NotEmpty(captured.ToolsConfiguration!.Tools);
    }

    [Fact]
    public async Task FilesOnly_Local_ChatReachesServiceWithFilesAndNoTools()
    {
        // Arrange
        Chat? captured = null;
        FakeFactory.Service.Handler = chat =>
        {
            captured = chat;
            return new ChatResult
            {
                Model = chat.ModelId ?? LocalModelId,
                Done = true,
                CreatedAt = DateTime.UtcNow,
                Message = new Message { Role = "assistant", Content = "ok", Type = MessageType.LocalLLM }
            };
        };

        var files = new List<FileInfo>
        {
            new() { Name = "doc.txt", Extension = ".txt", Content = "Some document content." }
        };

        // Act
        var result = await AIHub.Chat()
            .WithModel(LocalModelId)
            .WithMessage("Summarise the document.")
            .WithFiles(files)
            .CompleteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(captured);
        Assert.IsType<LocalInferenceParams>(captured!.BackendParams);
        var lastMsg = captured.Messages.Last(m => m.Role == "User");
        Assert.NotEmpty(lastMsg.Files ?? []);
        Assert.True(captured.ToolsConfiguration is null || captured.ToolsConfiguration.Tools.Count == 0);
    }
}
