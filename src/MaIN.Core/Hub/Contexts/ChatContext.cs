using MaIN.Core.Hub.Contexts.Interfaces.ChatContext;
using MaIN.Core.Hub.Contexts.Interfaces.ModelContext;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions.Chats;
using MaIN.Domain.Exceptions.Models;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.Models;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.Hub.Contexts;

public sealed class ChatContext : IChatBuilderEntryPoint, IChatMessageBuilder, IChatConfigurationBuilder
{
    private readonly IChatService _chatService;
    private readonly IModelContext _modelContext;
    private bool _preProcess;
    private bool _ensureModelDownloaded;
    private readonly Chat _chat;
    private List<FileInfo> _files = [];

    internal ChatContext(IChatService chatService, IModelContext modelContext)
    {
        _chatService = chatService;
        _modelContext = modelContext;
        _chat = new Chat
        {
            Name = "New Chat",
            Id = Guid.NewGuid().ToString(),
            Messages = [],
            ModelId = string.Empty
        };
    }

    internal ChatContext(IChatService chatService, IModelContext modelContext, Chat existingChat)
    {
        _chatService = chatService;
        _modelContext = modelContext;
        _chat = existingChat;
    }

    public IChatMessageBuilder WithModel(string modelId)
    {
        if (!ModelRegistry.Exists(modelId))
        {
            throw new ModelNotRegisteredException(modelId);
        }

        _chat.ModelId = modelId;
        _chat.ImageGen = ModelRegistry.GetById(modelId).HasImageGeneration;
        return this;
    }

    public IChatMessageBuilder EnsureModelDownloaded()
    {
        _ensureModelDownloaded = true;
        return this;
    }

    public IChatConfigurationBuilder WithInferenceParams(IBackendInferenceParams inferenceParams)
    {
        _chat.BackendParams = inferenceParams;
        return this;
    }

    public IChatConfigurationBuilder WithTools(ToolsConfiguration toolsConfiguration)
    {
        _chat.ToolsConfiguration = toolsConfiguration;
        return this;
    }

    public IChatConfigurationBuilder WithMemoryParams(MemoryParams memoryParams)
    {
        _chat.MemoryParams = memoryParams;
        return this;
    }

    public IChatConfigurationBuilder Speak(TextToSpeechParams speechParams)
    {
        _chat.ImageGen = false;
        _chat.TextToSpeechParams = speechParams;
        return this;
    }

    public IChatConfigurationBuilder WithSystemPrompt(string systemPrompt)
    {
        var message = new Message
        {
            Role = "System",
            Content = systemPrompt,
            Type = MessageType.NotSet,
            Time = DateTime.Now
        };

        _chat.Messages.Insert(0, message);
        return this;
    }

    public IChatConfigurationBuilder WithMessage(string content)
    {
        var message = new Message
        {
            Role = "User",
            Content = content,
            Type = MessageType.NotSet,
            Time = DateTime.Now
        };

        _chat.Messages.Add(message);
        return this;
    }

    public IChatConfigurationBuilder WithMessage(string content, byte[] image)
    {
        var message = new Message
        {
            Role = "User",
            Content = content,
            Type = MessageType.NotSet,
            Time = DateTime.Now,
            Image = image
        };

        _chat.Messages.Add(message);
        return this;
    }

    public IChatConfigurationBuilder WithMessages(IEnumerable<Message> messages)
    {
        _chat.Messages.AddRange(messages);
        return this;
    }

    public IChatConfigurationBuilder WithFiles(List<FileStream> file, bool preProcess = false)
    {
        _files = [.. file.Select(f => new FileInfo
        {
            Name = Path.GetFileName(f.Name),
            StreamContent = f,
            Extension = Path.GetExtension(f.Name)
        })];

        _preProcess = preProcess;
        return this;
    }

    public IChatConfigurationBuilder WithFiles(List<FileInfo> file, bool preProcess = false)
    {
        _files = file;
        _preProcess = preProcess;
        return this;
    }

    public IChatConfigurationBuilder WithFiles(List<string> file, bool preProcess = false)
    {
        _files = [.. file
            .Select(path =>
                new FileInfo
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Extension = Path.GetExtension(path)
                })];
        _preProcess = preProcess;
        return this;
    }

    public IChatConfigurationBuilder DisableCache()
    {
        _chat.Properties.AddProperty(ServiceConstants.Properties.DisableCacheProperty);
        return this;
    }

    public async Task<ChatResult> CompleteAsync(
        bool translate = false, // Move to WithTranslate
        bool interactive = false, // Move to WithInteractive
        Func<LLMTokenValue?, Task>? changeOfValue = null,
        Func<ToolInvocation, Task>? toolCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_chat.ModelId))
        {
            throw new MissingModelIdException(nameof(_chat.ModelId));
        }

        if (_chat.Messages.Count == 0)
        {
            throw new EmptyChatException(_chat.Id);
        }

        if (_ensureModelDownloaded)
        {
            await _modelContext.EnsureDownloadedAsync(_chat.ModelId, cancellationToken);
        }

        _chat.Messages.Last().Files = _files;
        if (_preProcess)
        {
            _chat.Messages.Last().Properties.AddProperty(ServiceConstants.Properties.PreProcessProperty);
        }

        if (!await ChatExists(_chat.Id))
        {
            await _chatService.Create(_chat);
        }

        var result = await _chatService.Completions(
            _chat,
            translate,
            interactive,
            changeOfValue,
            toolCallback,
            cancellationToken);
        _files = [];
        return result;
    }

    public async Task<IChatConfigurationBuilder> FromExisting(string chatId)
    {
        var existing = await _chatService.GetById(chatId);
        return existing is null
            ? throw new ChatNotFoundException(chatId)
            : new ChatContext(_chatService, _modelContext, existing);
    }

    private async Task<bool> ChatExists(string id)
    {
        try
        {
            await _chatService.GetById(id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetChatId() => _chat.Id;

    public async Task<Chat> GetCurrentChat()
    {
        return _chat.Id is null
            ? throw new ChatNotInitializedException()
            : await _chatService.GetById(_chat.Id);
    }

    public async Task<List<Chat>> GetAllChats() => await _chatService.GetAll();

    public async Task DeleteChat()
    {
        if (_chat.Id is null)
        {
            throw new ChatNotInitializedException();
        }

        await _chatService.Delete(_chat.Id);
    }

    public List<MessageShort> GetChatHistory()
    {
        return [.. _chat.Messages.Select(x => new MessageShort()
        {
            Content = x.Content,
            Role = x.Role,
            Time = x.Time
        })];
    }
}
