using System.Text;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.Models;
using Microsoft.Extensions.Logging;

namespace MaIN.Services.Services.LLMService;

public sealed class OllamaService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<OpenAiService>? logger = null)
    : OpenAiCompatibleService(notificationService, httpClientFactory, memoryFactory, memoryService, logger)
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    private bool HasApiKey => !string.IsNullOrEmpty(_settings.OllamaKey) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LLMApiRegistry.Ollama.ApiKeyEnvName));

    private string LocalBaseUrl => (_settings.OllamaBaseUrl ?? "http://localhost:11434").TrimEnd('/');

    protected override string HttpClientName => HasApiKey ? ServiceConstants.HttpClients.OllamaClient : ServiceConstants.HttpClients.OllamaLocalClient;
    protected override string ChatCompletionsUrl => HasApiKey ? ServiceConstants.ApiUrls.OllamaOpenAiChatCompletions : $"{LocalBaseUrl}/v1/chat/completions";
    protected override string ModelsUrl => HasApiKey ? ServiceConstants.ApiUrls.OllamaModels : $"{LocalBaseUrl}/v1/models";
    protected override Type ExpectedParamsType => typeof(OllamaInferenceParams);
    protected override bool SupportsSemanticSearch => false;

    protected override string GetApiKey()
    {
        return _settings.OllamaKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Ollama.ApiKeyEnvName) ?? string.Empty;
    }

    protected override string GetApiName() => LLMApiRegistry.Ollama.ApiName;

    protected override void ValidateApiKey()
    {
        // No validation required - local Ollama doesn't need an API key
        // Cloud Ollama will fail at runtime if the key is missing
    }

    protected override void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams is not OllamaInferenceParams p) return;
        if (p.Temperature.HasValue) requestBody["temperature"] = p.Temperature.Value;
        if (p.MaxTokens.HasValue) requestBody["max_tokens"] = p.MaxTokens.Value;
        if (p.TopP.HasValue) requestBody["top_p"] = p.TopP.Value;
        if (p.TopK.HasValue) requestBody["top_k"] = p.TopK.Value;
        if (p.NumCtx.HasValue || p.NumGpu.HasValue)
        {
            var options = new Dictionary<string, object>();
            if (p.NumCtx.HasValue) options["num_ctx"] = p.NumCtx.Value;
            if (p.NumGpu.HasValue) options["num_gpu"] = p.NumGpu.Value;
            requestBody["options"] = options;
        }
    }

    public override async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        var lastMsg = chat.Messages.Last();
        var filePaths = await DocumentProcessor.ConvertToFilesContent(memoryOptions);
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