using MaIN.Core.Hub.Contexts;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.Models;
using Moq;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.UnitTests;

public class ChatContextTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly ChatContext _chatContext;
    private readonly string _testModelId = "test-model";

    private static ModelContext CreateModelContext() =>
        new(new MaINSettings(), Mock.Of<IHttpClientFactory>());

    public ChatContextTests()
    {
        _mockChatService = new Mock<IChatService>();
        _chatContext = new ChatContext(_mockChatService.Object, CreateModelContext());
        var testModel = new GenericLocalModel(_testModelId);
        ModelRegistry.RegisterOrReplace(testModel);
    }

    [Fact]
    public void Constructor_ShouldInitializeNewChat()
    {
        // Act
        var chatId = _chatContext.GetChatId();

        // Assert
        Assert.NotNull(chatId);
        Assert.NotEmpty(chatId);
    }

    [Fact]
    public void WithMessage_ShouldAddMessage()
    {
        // Act
        _chatContext.WithMessage("Hello, world!");
        var messages = _chatContext.GetChatHistory();

        // Assert
        Assert.Single(messages);
        Assert.Equal("Hello, world!", messages[0].Content);
        Assert.Equal("User", messages[0].Role);
    }

    [Fact]
    public void WithSystemPrompt_ShouldInsertSystemMessageAtBeginning()
    {
        // Act
        _chatContext.WithMessage("User message");
        _chatContext.WithSystemPrompt("System prompt");
        var messages = _chatContext.GetChatHistory();

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal("System", messages[0].Role);
        Assert.Equal("System prompt", messages[0].Content);
    }

    [Fact]
    public void WithFiles_ShouldAttachFilesToLastMessage()
    {
        // Arrange
        _chatContext.WithMessage("User message");
        var files = new List<FileInfo>
        {
            new() { Name = "file.txt", Path = "/path/file.txt", Extension = "txt" }
        };

        // Act
        _chatContext.WithFiles(files);

        // Assert
        var lastMessage = _chatContext.GetChatHistory().Last();
        Assert.NotNull(lastMessage);
    }

    [Fact]
    public async Task CompleteAsync_ShouldCallChatService()
    {
        // Arrange
        var chatResult = new ChatResult()
        {
            Model = "test-model",
            Message = new Message
            {
                Role = "Assistant",
                Content = "test-message",
                Type = MessageType.LocalLLM
            }
        };

        _mockChatService
            .Setup(s => s.Completions(
                It.IsAny<Chat>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResult);

        _chatContext.WithMessage("User message");
        _chatContext.WithModel(_testModelId);

        // Act
        var result = await _chatContext.CompleteAsync();

        // Assert
        _mockChatService.Verify(
            s => s.Completions(
                It.IsAny<Chat>(),
                false,
                false,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(chatResult, result);
    }

    [Fact]
    public async Task GetCurrentChat_ShouldCallChatService()
    {
        // Arrange
        var chat = new Chat { Id = _chatContext.GetChatId(), ModelId = _testModelId, Name = "test" };
        _mockChatService.Setup(s => s.GetById(chat.Id)).ReturnsAsync(chat);

        // Act
        var result = await _chatContext.GetCurrentChat();

        // Assert
        Assert.Equal(chat, result);
    }

    [Fact]
    public async Task WithModel_ShouldSetModelIdAndInstance()
    {
        // Act
        await _chatContext.WithModel(_testModelId)
            .WithMessage("User message")
            .CompleteAsync();

        // Assert
        _mockChatService.Verify(s =>
            s.Completions(
                It.Is<Chat>(c => c.ModelId == _testModelId),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_ShouldPassToolCallbackThrough()
    {
        // Arrange
        var chatResult = new ChatResult()
        {
            Model = "test-model",
            Message = new Message
            {
                Role = "Assistant",
                Content = "test-message",
                Type = MessageType.LocalLLM
            }
        };

        Func<MaIN.Domain.Entities.Tools.ToolInvocation, Task>? capturedCallback = null;

        _mockChatService
            .Setup(s => s.Completions(
                It.IsAny<Chat>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                null,
                It.IsAny<Func<MaIN.Domain.Entities.Tools.ToolInvocation, Task>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Chat, bool, bool, Func<LLMTokenValue?, Task>?, Func<MaIN.Domain.Entities.Tools.ToolInvocation, Task>?, CancellationToken>(
                (_, _, _, _, toolCallback, _) => capturedCallback = toolCallback)
            .ReturnsAsync(chatResult);

        _chatContext.WithModel(_testModelId).WithMessage("User message");

        var receivedInvocations = new List<MaIN.Domain.Entities.Tools.ToolInvocation>();
        Task MyToolCallback(MaIN.Domain.Entities.Tools.ToolInvocation invocation)
        {
            receivedInvocations.Add(invocation);
            return Task.CompletedTask;
        }

        // Act
        await _chatContext.CompleteAsync(toolCallback: MyToolCallback);
        Assert.NotNull(capturedCallback);
        // Invoke whatever delegate actually reached IChatService.Completions and confirm it's
        // the same callback (proven via its side effect, not delegate reference/object equality,
        // which is unreliable for local-function-to-delegate conversions).
        await capturedCallback!(new MaIN.Domain.Entities.Tools.ToolInvocation { ToolName = "test_tool", Arguments = "{}", Done = false });

        // Assert
        Assert.Single(receivedInvocations);
        Assert.Equal("test_tool", receivedInvocations[0].ToolName);
    }
}
