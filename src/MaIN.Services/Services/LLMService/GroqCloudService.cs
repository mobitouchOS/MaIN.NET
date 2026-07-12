using System.Text;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Services.Abstract;
using Microsoft.Extensions.Logging;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Constants;
using MaIN.Services.Services.Models;
using MaIN.Domain.Configuration.BackendInferenceParams;

namespace MaIN.Services.Services.LLMService;

public sealed class GroqCloudService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<OpenAiService>? logger = null)
    : OpenAiCompatibleService(notificationService, httpClientFactory, memoryFactory, memoryService, logger)
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    protected override string HttpClientName => ServiceConstants.HttpClients.GroqCloudClient;
    protected override string ChatCompletionsUrl => ServiceConstants.ApiUrls.GroqCloudOpenAiChatCompletions;
    protected override string ModelsUrl => ServiceConstants.ApiUrls.GroqCloudModels;
    protected override Type ExpectedParamsType => typeof(GroqCloudInferenceParams);
    protected override bool SupportsSemanticSearch => false;

    protected override string GetApiKey()
    {
        return _settings.GroqCloudKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Groq.ApiKeyEnvName) ??
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Groq.ApiName);
    }

    protected override string GetApiName() => LLMApiRegistry.Groq.ApiName;

    protected override void ValidateApiKey()
    {
        if (string.IsNullOrEmpty(_settings.GroqCloudKey) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LLMApiRegistry.Groq.ApiKeyEnvName)))
        {
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Groq.ApiName);
        }
    }

    protected override void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams is not GroqCloudInferenceParams p) return;
        if (p.Temperature.HasValue) requestBody["temperature"] = p.Temperature.Value;
        if (p.MaxTokens.HasValue) requestBody["max_tokens"] = p.MaxTokens.Value;
        if (p.TopP.HasValue) requestBody["top_p"] = p.TopP.Value;
        if (p.FrequencyPenalty.HasValue) requestBody["frequency_penalty"] = p.FrequencyPenalty.Value;
        if (p.ResponseFormat != null) requestBody["response_format"] = new { type = p.ResponseFormat };
    }

    public override async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        var lastMsg = chat.Messages.Last();
        var filePaths = await DocumentProcessor.ConvertToFilesContent(memoryOptions, cancellationToken);
        var message = new Message()
        {
            Role = ServiceConstants.Roles.User,
            Content = ComposeMessage(lastMsg, filePaths),
            Type = MessageType.CloudLLM
        };

        chat.Messages.Last().Content = message.Content;
        chat.Messages.Last().Files = [];
        var result = await Send(chat, requestOptions, cancellationToken);
        chat.Messages.Last().Content = lastMsg.Content;
        return result;
    }

    private string ComposeMessage(Message lastMsg, string[] filePaths)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("== FILES IN MEMORY");
        foreach (var path in filePaths)
        {
            var doc = DocumentProcessor.ProcessDocument(path);
            stringBuilder.Append(doc);
            stringBuilder.AppendLine();
        }
        stringBuilder.AppendLine("== END OF FILES");
        stringBuilder.AppendLine();
        stringBuilder.Append(lastMsg.Content);
        return stringBuilder.ToString();
    }
}
