using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.Models;
using MaIN.Services.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace MaIN.Services.Services.LLMService;

public sealed class GeminiService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<GeminiService>? logger = null)
    : OpenAiCompatibleService(notificationService, httpClientFactory, memoryFactory, memoryService, logger)
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    private readonly IHttpClientFactory _httpClientFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    protected override string HttpClientName => ServiceConstants.HttpClients.GeminiClient;
    protected override string ChatCompletionsUrl => ServiceConstants.ApiUrls.GeminiOpenAiChatCompletions;
    protected override Type ExpectedParamsType => typeof(GeminiInferenceParams);

    public override async Task<string[]> GetCurrentModels()
    {
        ValidateApiKey();

        var client = _httpClientFactory.CreateClient(ServiceConstants.HttpClients.GeminiClient);
        client.DefaultRequestHeaders.Add("x-goog-api-key", [GetApiKey()]);

        using var response = await client.GetAsync(ServiceConstants.ApiUrls.GeminiModels);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var modelsResponse = JsonSerializer.Deserialize<GeminiModelsResponse>(responseJson);

        return modelsResponse?.Models?
                   .Where(m => m.Name!.StartsWith("models/gemini", StringComparison.InvariantCultureIgnoreCase))
                   .Select(m => m.Name![7..]) // remove "models/" part => get baseModelId
                   .ToArray()
               ?? [];
    }

    protected override string GetApiKey()
    {
        return _settings.GeminiKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Gemini.ApiKeyEnvName) ??
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Gemini.ApiName);
    }

    protected override string GetApiName() => LLMApiRegistry.Gemini.ApiName;

    protected override void ValidateApiKey()
    {
        if (string.IsNullOrEmpty(_settings.GeminiKey) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LLMApiRegistry.Gemini.ApiKeyEnvName)))
        {
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Gemini.ApiName);
        }
    }

    protected override void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams is not GeminiInferenceParams p)
        {
            return;
        }

        if (p.Temperature.HasValue)
        {
            requestBody["temperature"] = p.Temperature.Value;
        }

        if (p.MaxTokens.HasValue)
        {
            requestBody["max_tokens"] = p.MaxTokens.Value;
        }

        if (p.TopP.HasValue)
        {
            requestBody["top_p"] = p.TopP.Value;
        }

        if (p.StopSequences is { Length: > 0 })
        {
            requestBody["stop"] = p.StopSequences;
        }
    }

    private protected override async Task<IIngestedMemory> CreateIngestedMemoryAsync(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        CancellationToken ct = default)
    {
        var kernel = _memoryFactory.CreateMemoryWithGemini(GetApiKey(), chat.MemoryParams);
        await _memoryService.ImportDataToMemory((kernel, null), memoryOptions, ct);
        return new IngestedMemory(kernel, () => kernel.DeleteIndexAsync());
    }

    public override async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        if (!chat.Messages.Any())
        {
            return null;
        }

        var userQuery = chat.Messages.Last().Content;
        if (chat.MemoryParams.Grammar != null)
        {
            var jsonGrammarConverter = new GrammarToJsonConverter();
            var jsonGrammar = jsonGrammarConverter.ConvertToJson(chat.MemoryParams.Grammar);
            userQuery =
                $"{userQuery} | For your next response only, please respond using exactly the following JSON format: \n{jsonGrammar}\n. Do not include any explanations, code blocks, or additional content. After this single JSON response, resume your normal conversational style.";
        }

        var lastMessage = chat.Messages.Last();
        if (lastMessage.Images?.Count > 0)
        {
            SearchResult searchResult;
            await using (var ingestedMemory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken))
            {
                searchResult = await ingestedMemory.Memory.SearchAsync(userQuery, cancellationToken: cancellationToken);
            }

            var ctxBuilder = new StringBuilder();
            foreach (var citation in searchResult.Results.SelectMany(r => r.Partitions))
            {
                ctxBuilder.AppendLine(citation.Text);
            }

            var originalContent = lastMessage.Content;
            if (ctxBuilder.Length > 0)
            {
                lastMessage.Content =
                    $"Use the following context to answer the question:\n\n{ctxBuilder}\n\nQuestion: {originalContent}";
            }

            lastMessage.Files = null;

            var result = await Send(chat, requestOptions, cancellationToken);
            lastMessage.Content = originalContent;
            return result;
        }

        await using var memory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken);
        var kernel = memory.Memory;
        MemoryAnswer retrievedContext;

        if (requestOptions.InteractiveUpdates || requestOptions.TokenCallback != null)
        {
            var responseBuilder = new StringBuilder();

            var searchOptions = new SearchOptions
            {
                Stream = true
            };

            await foreach (var chunk in kernel.AskStreamingAsync(
                               userQuery,
                               options: searchOptions,
                               cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Result))
                {
                    responseBuilder.Append(chunk.Result);

                    var tokenValue = new LLMTokenValue
                    {
                        Text = chunk.Result,
                        Type = TokenType.Message
                    };

                    if (requestOptions.InteractiveUpdates)
                    {
                        await _notificationService.DispatchNotification(
                            NotificationMessageBuilder.CreateChatCompletion(chat.Id, tokenValue, false),
                            ServiceConstants.Notifications.ReceiveMessageUpdate);
                    }

                    requestOptions.TokenCallback?.Invoke(tokenValue);
                }
            }

            retrievedContext = new MemoryAnswer
            {
                Question = userQuery,
                Result = responseBuilder.ToString(),
                NoResult = responseBuilder.Length == 0
            };
        }
        else
        {
            var searchOptions = new SearchOptions
            {
                Stream = false
            };

            retrievedContext = await kernel.AskAsync(
                userQuery,
                options: searchOptions,
                cancellationToken: cancellationToken);
        }

        chat.Messages.Last().MarkProcessed();
        return CreateChatResult(chat, retrievedContext.Result, []);
    }
}

file class GeminiModelsResponse
{
    [JsonPropertyName("models")] public List<GeminiModel>? Models { get; set; }
}

file class GeminiModel
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}
