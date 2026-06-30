using MaIN.Domain.Configuration;
using MaIN.Services.Constants;
using MaIN.Services.Services.Models;
using MaIN.Domain.Entities;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;
using MaIN.Domain.Configuration.BackendInferenceParams;

namespace MaIN.Services.Services.LLMService;

public sealed class XaiService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<OpenAiService>? logger = null)
    : OpenAiCompatibleService(notificationService, httpClientFactory, memoryFactory, memoryService, logger)
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    protected override string HttpClientName => ServiceConstants.HttpClients.XaiClient;
    protected override string ChatCompletionsUrl => ServiceConstants.ApiUrls.XaiOpenAiChatCompletions;
    protected override string ModelsUrl => ServiceConstants.ApiUrls.XaiModels;
    protected override Type ExpectedParamsType => typeof(XaiInferenceParams);
    protected override bool SupportsSemanticSearch => false;

    protected override string GetApiKey()
    {
        return _settings.XaiKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Xai.ApiKeyEnvName) ??
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Xai.ApiName);
    }
    
    protected override string GetApiName() => LLMApiRegistry.Xai.ApiName;

    protected override void ValidateApiKey()
    {
        if (string.IsNullOrEmpty(_settings.XaiKey) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LLMApiRegistry.Xai.ApiKeyEnvName)))
        {
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Xai.ApiName);
        }
    }

    protected override void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams is not XaiInferenceParams p) return;
        if (p.Temperature.HasValue) requestBody["temperature"] = p.Temperature.Value;
        if (p.MaxTokens.HasValue) requestBody["max_tokens"] = p.MaxTokens.Value;
        if (p.TopP.HasValue) requestBody["top_p"] = p.TopP.Value;
        if (p.FrequencyPenalty.HasValue) requestBody["frequency_penalty"] = p.FrequencyPenalty.Value;
        if (p.PresencePenalty.HasValue) requestBody["presence_penalty"] = p.PresencePenalty.Value;
    }

    protected override LLMTokenValue? ProcessChatCompletionChunk(string data)
    {
        var chunk = JsonSerializer.Deserialize<XaiCompletionChunk>(data,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Streaming delta — regular content
        var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
        if (!string.IsNullOrEmpty(content))
            return new LLMTokenValue { Text = content, Type = TokenType.Message };

        // Streaming delta — incremental reasoning (grok-4.20-reasoning style)
        var deltaReasoning = chunk?.Choices?.FirstOrDefault()?.Delta?.ReasoningContent;
        if (!string.IsNullOrEmpty(deltaReasoning))
            return new LLMTokenValue { Text = deltaReasoning, Type = TokenType.Reason };

        // Final completion event — encrypted reasoning blob (grok-4-1-fast-reasoning style)
        // message.content is intentionally ignored (already assembled from streaming chunks above)
        var encryptedReasoning = chunk?.Reasoning?.EncryptedContent;
        if (!string.IsNullOrEmpty(encryptedReasoning))
            return new LLMTokenValue { Text = encryptedReasoning, Type = TokenType.Reason };

        return null;
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
        stringBuilder.AppendLine($"== FILES IN MEMORY");
        foreach (var path in filePaths)
        {
            var doc = DocumentProcessor.ProcessDocument(path);
            stringBuilder.Append(doc);
            stringBuilder.AppendLine();
        }
        stringBuilder.AppendLine($"== END OF FILES");
        stringBuilder.AppendLine();
        stringBuilder.Append(lastMsg.Content);
        return stringBuilder.ToString();
    }
}

file class XaiCompletionChunk
{
    public List<XaiChoiceChunk>? Choices { get; set; }
    public XaiReasoning? Reasoning { get; set; }
}

file class XaiChoiceChunk
{
    public XaiDelta? Delta { get; set; }
}

file class XaiDelta
{
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}

file class XaiReasoning
{
    [JsonPropertyName("encrypted_content")]
    public string? EncryptedContent { get; set; }
}