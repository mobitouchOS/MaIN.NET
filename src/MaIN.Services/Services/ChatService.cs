using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions.Chats;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Repositories;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService;
using MaIN.Services.Services.LLMService.Factory;
using MaIN.Services.Services.Models;

namespace MaIN.Services.Services;

public class ChatService(
    ITranslatorService translatorService,
    IChatRepository chatProvider,
    ILLMServiceFactory llmServiceFactory,
    IImageGenServiceFactory imageGenServiceFactory,
    ITTSServiceFactory ttsServiceFactory,
    MaINSettings settings) : IChatService
{
    public async Task Create(Chat chat)
    {
        chat.Type = ChatType.Conversation;
        await chatProvider.AddChat(chat);
    }

    public async Task<ChatResult> Completions(
        Chat chat,
        bool translate = false,
        bool interactiveUpdates = false,
        Func<LLMTokenValue?, Task>? changeOfValue = null,
        CancellationToken cancellationToken = default,
        Func<ToolInvocation, Task>? toolCallback = null)
    {
        if (!ModelRegistry.TryGetById(chat.ModelId, out var model))
        {
            throw new ChatModelNotAvailableException(chat.Id, chat.ModelId);
        }

        if (model!.HasImageGeneration)
        {
            chat.ImageGen = true;
        }

        var backend = model!.Backend;
        chat.BackendParams ??= BackendParamsFactory.Create(backend);

        chat.Messages.Where(x => x.Type == MessageType.NotSet).ToList()
            .ForEach(x => x.Type = backend != BackendType.Self ? MessageType.CloudLLM : MessageType.LocalLLM);

        translate = translate || chat.Translate;
        interactiveUpdates = interactiveUpdates || chat.Interactive;
        var newMsg = chat.Messages.Last();
        newMsg.Time = DateTime.Now;

        var lng = translate ? await translatorService.DetectLanguage(newMsg.Content) : null;
        var originalMessages = chat.Messages;

        if (translate)
        {
            chat.Messages = [.. await Task.WhenAll(chat.Messages.Select(async m => new Message()
            {
                Role = m.Role,
                Content = await translatorService.Translate(m.Content, "en"),
                Type = m.Type
            }))];
        }

        var result = chat.ImageGen
            ? await imageGenServiceFactory.CreateService(backend)!.Send(chat)
            : await llmServiceFactory.CreateService(backend).Send(chat, new ChatRequestOptions()
            {
                InteractiveUpdates = interactiveUpdates,
                TokenCallback = changeOfValue,
                ToolCallback = toolCallback
            }, cancellationToken);

        if (translate)
        {
            result!.Message.Content = await translatorService.Translate(result.Message.Content, lng!);
            result.Message.Time = DateTime.Now;
        }

        if (!chat.ImageGen && chat.TextToSpeechParams is not null)
        {
            var speechBytes = await ttsServiceFactory
                .CreateService(backend).Send(result!.Message, chat.TextToSpeechParams.Model,
                    chat.TextToSpeechParams.Voice, chat.TextToSpeechParams.Playback);

            result.Message.Speech = speechBytes;
        }

        originalMessages.Add(result!.Message);
        chat.Messages = originalMessages;

        await chatProvider.UpdateChat(chat.Id!, chat);
        return result;
    }

    public async Task Delete(string id)
    {
        var chat = await chatProvider.GetChatById(id);
        var backend = chat is not null && ModelRegistry.TryGetById(chat.ModelId, out var model)
            ? model!.Backend
            : settings.BackendType;
        var llmService = llmServiceFactory.CreateService(backend);
        await llmService.CleanSessionCache(id);
        await chatProvider.DeleteChat(id);
    }

    public async Task<Chat> GetById(string id)
    {
        var chat = await chatProvider.GetChatById(id);
        return chat is null ? throw new ChatNotFoundException(id) : chat;
    }

    public async Task<List<Chat>> GetAll() => [.. await chatProvider.GetAllChats()];
}
