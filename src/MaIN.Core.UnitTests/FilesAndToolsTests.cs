using MaIN.Core.Hub.Contexts;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.Models;
using Moq;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.UnitTests;

public class FilesAndToolsTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly ChatContext _chatContext;
    private Chat? _sentChat;

    private static ModelContext CreateModelContext() =>
        new(new MaINSettings(), Mock.Of<IHttpClientFactory>());

    public FilesAndToolsTests()
    {
        _mockChatService = new Mock<IChatService>();
        _chatContext = new ChatContext(_mockChatService.Object, CreateModelContext());

        var testModel = new GenericLocalModel("test-model");
        ModelRegistry.RegisterOrReplace(testModel);
        _chatContext.WithModel("test-model");

        SetupChatService();
    }

    private static ChatResult CreateChatResult() =>
        new()
        {
            Model = "test-model",
            Message = new Message
            {
                Role = "Assistant",
                Content = "response",
                Type = MessageType.LocalLLM
            }
        };

    private void SetupChatService()
    {
        _mockChatService
            .Setup(s => s.Completions(
                It.IsAny<Chat>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Func<LLMTokenValue?, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Chat, bool, bool, Func<LLMTokenValue?, Task>?, CancellationToken>(
                (chat, _, _, _, _) => _sentChat = chat)
            .ReturnsAsync(CreateChatResult());
    }

    [Fact]
    public async Task WithFilesAndTools_BothArePresentOnChatPassedToService()
    {
        var files = new List<FileInfo>
        {
            new() { Name = "doc.pdf", Path = "/path/doc.pdf", Extension = "pdf" }
        };
        var tools = new ToolsConfigurationBuilder()
            .AddTool("get_date", "Get today's date", () => "2026-06-29")
            .Build();

        _chatContext
            .WithMessage("What date is it and what does the doc say?")
            .WithFiles(files)
            .WithTools(tools);

        await _chatContext.CompleteAsync();

        Assert.NotNull(_sentChat);
        var lastMessage = _sentChat.Messages.Last();
        Assert.NotNull(lastMessage.Files);
        Assert.NotEmpty(lastMessage.Files);
        Assert.NotNull(_sentChat.ToolsConfiguration);
        Assert.NotEmpty(_sentChat.ToolsConfiguration.Tools);
    }

    [Fact]
    public async Task WithFilesOnly_NoToolsOnChatPassedToService()
    {
        var files = new List<FileInfo>
        {
            new() { Name = "doc.pdf", Path = "/path/doc.pdf", Extension = "pdf" }
        };

        _chatContext
            .WithMessage("Summarise the document.")
            .WithFiles(files);

        await _chatContext.CompleteAsync();

        Assert.NotNull(_sentChat);
        var hasTools = _sentChat.ToolsConfiguration?.Tools is { Count: > 0 };
        Assert.False(hasTools);
    }

    [Fact]
    public async Task WithToolsOnly_NoFilesOnChatPassedToService()
    {
        var tools = new ToolsConfigurationBuilder()
            .AddTool("get_date", "Get today's date", () => "2026-06-29")
            .Build();

        _chatContext
            .WithMessage("What day is it?")
            .WithTools(tools);

        await _chatContext.CompleteAsync();

        Assert.NotNull(_sentChat);
        var lastMessage = _sentChat.Messages.Last();
        var hasFiles = lastMessage.Files is { Count: > 0 };
        Assert.False(hasFiles);
    }
}
