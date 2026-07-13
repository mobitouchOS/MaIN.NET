using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using LLama.Common;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Utils;
using MaIN.Services.Services.Models;
using MaIN.Services.Utils;
using Microsoft.Extensions.Logging;

namespace MaIN.Services.Services.LLMService;

public sealed class AnthropicService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    ILogger<AnthropicService>? logger = null)
    : ILLMService
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    private static readonly ConcurrentDictionary<string, List<ChatMessage>> SessionCache = new();

    private const string CompletionsUrl = ServiceConstants.ApiUrls.AnthropicChatMessages;
    private const string ModelsUrl = ServiceConstants.ApiUrls.AnthropicModels;
    private const int MaxToolIterations = 5;

    private static bool HasAnthropicSkills(Chat chat) =>
        chat.ProviderSkillReferences.Any(r => r.Backend == BackendType.Anthropic);

    private HttpClient CreateAnthropicHttpClient(bool requireSkillsBeta = false)
    {
        var client = httpClientFactory.CreateClient(ServiceConstants.HttpClients.AnthropicClient);
        client.DefaultRequestHeaders.Add("x-api-key", GetApiKey());
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        if (requireSkillsBeta)
        {
            client.DefaultRequestHeaders.Add("anthropic-beta", ServiceConstants.AnthropicBetaFeatures.SkillsBetaHeader);
        }

        return client;
    }

    private string GetApiKey()
    {
        return _settings.AnthropicKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Anthropic.ApiKeyEnvName) ??
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Anthropic.ApiName);
    }

    private void ValidateApiKey()
    {
        if (string.IsNullOrEmpty(_settings.AnthropicKey) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LLMApiRegistry.Anthropic.ApiKeyEnvName)))
        {
            throw new APIKeyNotConfiguredException(LLMApiRegistry.Anthropic.ApiName);
        }
    }

    public async Task<ChatResult?> Send(Chat chat, ChatRequestOptions options, CancellationToken cancellationToken = default)
    {
        if (chat.BackendParams != null && chat.BackendParams is not AnthropicInferenceParams)
        {
            throw new InvalidBackendParamsException(LLMApiRegistry.Anthropic.ApiName, nameof(AnthropicInferenceParams), chat.BackendParams.GetType().Name);
        }

        ValidateApiKey();

        if (!chat.Messages.Any())
        {
            return null;
        }

        var lastMessage = chat.Messages.Last();
        await ChatHelper.ExtractImageFromFiles(lastMessage);

        var conversation = GetOrCreateConversation(chat, options.CreateSession);
        var resultBuilder = new StringBuilder();
        var tokens = new List<LLMTokenValue>();

        if (ChatHelper.HasFiles(lastMessage))
        {
            var result = ChatHelper.ExtractMemoryOptions(lastMessage);
            var memoryResult = await AskMemory(chat, result, options, cancellationToken);
            resultBuilder.Append(memoryResult!.Message.Content);
            lastMessage.MarkProcessed();
            UpdateSessionCache(chat.Id, resultBuilder.ToString(), options.CreateSession);
            if (options.TokenCallback != null)
            {
                await options.TokenCallback(new LLMTokenValue()
                {
                    Text = resultBuilder.ToString(),
                    Type = TokenType.FullAnswer
                });
            }

            return CreateChatResult(chat, resultBuilder.ToString(), tokens);
        }

        if (chat.ToolsConfiguration?.Tools != null && chat.ToolsConfiguration.Tools.Any())
        {
            return await ProcessWithToolsAsync(
                chat,
                conversation,
                tokens,
                options,
                cancellationToken);
        }

        if (options.InteractiveUpdates || options.TokenCallback != null)
        {
            await ProcessStreamingChatAsync(
                chat,
                conversation,
                tokens,
                resultBuilder,
                options.TokenCallback,
                options.InteractiveUpdates,
                cancellationToken);
        }
        else
        {
            await ProcessNonStreamingChatAsync(
                chat,
                conversation,
                resultBuilder,
                cancellationToken);
        }

        var finalToken = new LLMTokenValue { Text = resultBuilder.ToString(), Type = TokenType.FullAnswer };
        tokens.Add(finalToken);

        if (options.InteractiveUpdates)
        {
            await notificationService.DispatchNotification(
                NotificationMessageBuilder.CreateChatCompletion(chat.Id, finalToken, true),
                ServiceConstants.Notifications.ReceiveMessageUpdate);
        }

        lastMessage.MarkProcessed();
        UpdateSessionCache(chat.Id, resultBuilder.ToString(), options.CreateSession);

        return CreateChatResult(chat, resultBuilder.ToString(), tokens);
    }

    private async Task<ChatResult> ProcessWithToolsAsync(
        Chat chat,
        List<ChatMessage> conversation,
        List<LLMTokenValue> tokens,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        StringBuilder resultBuilder = new();
        StringBuilder fullResponseBuilder = new();
        int iterations = 0;
        List<AnthropicToolUse>? currentToolUses = null;
        var maxToolIterations = chat.ToolsConfiguration?.MaxIterations ?? MaxToolIterations;

        while (iterations < maxToolIterations)
        {
            if (iterations > 0 && fullResponseBuilder.Length > 0)
            {
                var spaceToken = new LLMTokenValue { Text = " ", Type = TokenType.Message };
                tokens.Add(spaceToken);

                if (options.TokenCallback != null)
                {
                    await options.TokenCallback(spaceToken);
                }

                if (options.InteractiveUpdates)
                {
                    await notificationService.DispatchNotification(
                        NotificationMessageBuilder.CreateChatCompletion(chat.Id, spaceToken, false),
                        ServiceConstants.Notifications.ReceiveMessageUpdate);
                }
            }

            if (options.InteractiveUpdates || options.TokenCallback != null)
            {
                currentToolUses = await ProcessStreamingChatWithToolsAsync(
                    chat,
                    conversation,
                    tokens,
                    resultBuilder,
                    options,
                    cancellationToken);
            }
            else
            {
                currentToolUses = await ProcessNonStreamingChatWithToolsAsync(
                    chat,
                    conversation,
                    resultBuilder,
                    cancellationToken);
            }

            if (resultBuilder.Length > 0)
            {
                if (fullResponseBuilder.Length > 0)
                {
                    fullResponseBuilder.Append(" ");
                }

                fullResponseBuilder.Append(resultBuilder);
            }

            if (currentToolUses == null || !currentToolUses.Any())
            {
                break;
            }

            var assistantContent = new List<object>();
            if (resultBuilder.Length > 0)
            {
                assistantContent.Add(new { type = "text", text = resultBuilder.ToString() });
            }

            assistantContent.AddRange(currentToolUses.Select(tu => new
            {
                type = "tool_use",
                id = tu.Id,
                name = tu.Name,
                input = tu.Input
            }));

            conversation.Add(new ChatMessage(ServiceConstants.Roles.Assistant, assistantContent));

            if (chat.Properties.CheckProperty(ServiceConstants.Properties.ClientSideToolExecutionProperty))
            {
                foreach (var toolUse in currentToolUses)
                {
                    options.ToolCallback?.Invoke(new ToolInvocation()
                    {
                        ToolName = toolUse.Name,
                        Arguments = toolUse.Input.ToString() ?? string.Empty,
                        Done = false
                    });
                }

                break;
            }

            var toolResults = new List<object>();
            foreach (var toolUse in currentToolUses)
            {
                if (chat.Properties.CheckProperty(ServiceConstants.Properties.AgentIdProperty))
                {
                    await notificationService.DispatchNotification(
                        NotificationMessageBuilder.ProcessingTools(chat.Properties[ServiceConstants.Properties.AgentIdProperty],
                            string.Empty,
                            toolUse.Name),
                        ServiceConstants.Notifications.ReceiveAgentUpdate);
                }

                var executor = chat.ToolsConfiguration?.GetExecutor(toolUse.Name);

                if (executor == null)
                {
                    throw new InvalidOperationException($"No executor found for tool: {toolUse.Name}");
                }

                try
                {
                    var inputJson = JsonSerializer.Serialize(toolUse.Input);
                    options.ToolCallback?.Invoke(new ToolInvocation()
                    {
                        ToolName = toolUse.Name,
                        Arguments = toolUse.Input.ToString() ?? string.Empty,
                        Done = false
                    });
                    var toolResult = await executor(inputJson);
                    options.ToolCallback?.Invoke(new ToolInvocation()
                    {
                        ToolName = toolUse.Name,
                        Arguments = toolUse.Input.ToString() ?? string.Empty,
                        Done = true
                    });

                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = toolUse.Id,
                        content = toolResult
                    });
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error executing tool {ToolName}", toolUse.Name);

                    var errorResult = JsonSerializer.Serialize(new { error = ex.Message });
                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = toolUse.Id,
                        content = errorResult,
                        is_error = true
                    });
                }
            }

            conversation.Add(new ChatMessage(ServiceConstants.Roles.User, toolResults));

            resultBuilder.Clear();
            iterations++;
        }

        if (iterations >= maxToolIterations)
        {
            logger?.LogWarning("Maximum tool iterations ({MaxIterations}) reached for chat {ChatId}",
                maxToolIterations, chat.Id);
        }

        var finalResponse = fullResponseBuilder.ToString();
        var finalToken = new LLMTokenValue { Text = finalResponse, Type = TokenType.FullAnswer };
        tokens.Add(finalToken);

        if (options.InteractiveUpdates)
        {
            await notificationService.DispatchNotification(
                NotificationMessageBuilder.CreateChatCompletion(chat.Id, finalToken, true),
                ServiceConstants.Notifications.ReceiveMessageUpdate);
        }

        chat.Messages.Last().MarkProcessed();
        UpdateSessionCache(chat.Id, finalResponse, options.CreateSession);
        return CreateChatResult(chat, finalResponse, tokens);
    }

    private async Task<List<AnthropicToolUse>?> ProcessStreamingChatWithToolsAsync(
        Chat chat,
        List<ChatMessage> conversation,
        List<LLMTokenValue> tokens,
        StringBuilder resultBuilder,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        var httpClient = CreateAnthropicHttpClient(HasAnthropicSkills(chat));

        var requestBody = await BuildAnthropicRequestBody(chat, conversation, true);
        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl)
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var toolUseBuilders = new Dictionary<int, AnthropicToolUseBuilder>();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("event:"))
            {
                continue;
            }

            if (line.StartsWith("data:"))
            {
                var data = line.Substring("data:".Length).Trim();

                try
                {
                    var chunk = JsonSerializer.Deserialize<AnthropicStreamChunk>(data,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (chunk?.Type == "content_block_start")
                    {
                        if (chunk.ContentBlock?.Type == "tool_use")
                        {
                            toolUseBuilders[chunk.Index] = new AnthropicToolUseBuilder
                            {
                                Id = chunk.ContentBlock.Id ?? string.Empty,
                                Name = chunk.ContentBlock.Name ?? string.Empty
                            };
                        }
                    }
                    else if (chunk?.Type == "content_block_delta")
                    {
                        if (chunk.Delta?.Type == "text_delta" && !string.IsNullOrEmpty(chunk.Delta.Text))
                        {
                            var token = new LLMTokenValue
                            {
                                Text = chunk.Delta.Text,
                                Type = TokenType.Message
                            };
                            tokens.Add(token);
                            resultBuilder.Append(chunk.Delta.Text);

                            if (options.TokenCallback != null)
                            {
                                await options.TokenCallback(token);
                            }

                            if (options.InteractiveUpdates)
                            {
                                await notificationService.DispatchNotification(
                                    NotificationMessageBuilder.CreateChatCompletion(chat.Id, token, false),
                                    ServiceConstants.Notifications.ReceiveMessageUpdate);
                            }
                        }
                        else if (chunk.Delta?.Type == "input_json_delta")
                        {
                            if (toolUseBuilders.ContainsKey(chunk.Index) &&
                                !string.IsNullOrEmpty(chunk.Delta.PartialJson))
                            {
                                toolUseBuilders[chunk.Index].InputJson.Append(chunk.Delta.PartialJson);
                            }
                        }
                    }
                    else if (chunk?.Type == "message_stop")
                    {
                        break;
                    }
                }
                catch (JsonException ex)
                {
                    logger?.LogError(ex, "Failed to parse Anthropic chunk");
                }
            }
        }

        if (toolUseBuilders.Any())
        {
            return toolUseBuilders.Values.Select(b => b.Build()).ToList();
        }

        return null;
    }

    private async Task HandleApiError(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var errorResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var errorMessage = ExtractApiErrorMessage(errorResponseBody);

        throw new LLMApiException(LLMApiRegistry.Anthropic.ApiName, response.StatusCode, errorMessage ?? errorResponseBody);
    }

    private static string? ExtractApiErrorMessage(string json)
    {
        try
        {
            using var jasonDocument = JsonDocument.Parse(json);
            if (jasonDocument.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // If the response is not a valid JSON or doesn't match the expected schema,
            // we fall back to the raw response body in the calling method.
        }

        return null;
    }

    private async Task<List<AnthropicToolUse>?> ProcessNonStreamingChatWithToolsAsync(
        Chat chat,
        List<ChatMessage> conversation,
        StringBuilder resultBuilder,
        CancellationToken cancellationToken)
    {
        var httpClient = CreateAnthropicHttpClient(HasAnthropicSkills(chat));

        var requestBody = await BuildAnthropicRequestBody(chat, conversation, false);
        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await httpClient.PostAsync(CompletionsUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<AnthropicMessageResponse>(responseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var toolUses = new List<AnthropicToolUse>();

        if (chatResponse?.Content != null)
        {
            foreach (var contentBlock in chatResponse.Content)
            {
                if (contentBlock.Type == "text" && !string.IsNullOrEmpty(contentBlock.Text))
                {
                    resultBuilder.Append(contentBlock.Text);
                }
                else if (contentBlock.Type == "tool_use")
                {
                    toolUses.Add(new AnthropicToolUse
                    {
                        Id = contentBlock.Id!,
                        Name = contentBlock.Name!,
                        Input = contentBlock.Input!
                    });
                }
            }
        }

        return toolUses.Any() ? toolUses : null;
    }

    private async Task<Dictionary<string, object>> BuildAnthropicRequestBody(Chat chat, List<ChatMessage> conversation, bool stream)
    {
        var anthParams = chat.BackendParams as AnthropicInferenceParams;

        // Anthropic Messages API rejects messages with role="system" inside the messages array
        // ("Unexpected role 'system'. The Messages API accepts a top-level `system` parameter").
        // Lift the system message out and skip it when building the messages array.
        var systemMessage = conversation.FirstOrDefault(m =>
            m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
        var conversationWithoutSystem = systemMessage is null
            ? conversation
            : conversation.Where(m => !ReferenceEquals(m, systemMessage)).ToList();

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = chat.ModelId,
            ["max_tokens"] = anthParams?.MaxTokens ?? 4096,
            ["stream"] = stream,
            ["messages"] = await ChatHelper.BuildMessagesArray(conversationWithoutSystem, chat, ImageType.AsBase64)
        };

        if (anthParams != null)
        {
            if (anthParams.Temperature.HasValue)
            {
                requestBody["temperature"] = anthParams.Temperature.Value;
            }

            if (anthParams.TopP.HasValue)
            {
                requestBody["top_p"] = anthParams.TopP.Value;
            }

            if (anthParams.TopK.HasValue)
            {
                requestBody["top_k"] = anthParams.TopK.Value;
            }
        }

        if (chat.BackendParams?.AdditionalParams != null)
        {
            foreach (var (key, value) in chat.BackendParams.AdditionalParams)
            {
                requestBody[key] = value;
            }
        }

        if (systemMessage != null && systemMessage.Content is string systemContent)
        {
            requestBody["system"] = systemContent;
        }

        if (chat.InferenceGrammar is not null)
        {
            requestBody["system"] =
                $"Respond only using the following grammar format: \n{chat.InferenceGrammar.Value}\n. Do not add explanations, code tags, or any extra content.";
        }

        var anthropicSkills = chat.ProviderSkillReferences
            .Where(r => r.Backend == BackendType.Anthropic)
            .ToList();

        var toolsList = new List<object>();

        if (chat.ToolsConfiguration?.Tools != null && chat.ToolsConfiguration.Tools.Any())
        {
            toolsList.AddRange(chat.ToolsConfiguration.Tools.Select(t => (object)new
            {
                name = t.Function!.Name,
                description = t.Function.Description,
                input_schema = t.Function.Parameters
            }));
        }

        if (anthropicSkills.Count > 0)
        {
            toolsList.Add(new
            {
                type = ServiceConstants.AnthropicBetaFeatures.CodeExecutionToolType,
                name = ServiceConstants.AnthropicBetaFeatures.CodeExecutionToolName
            });

            requestBody["container"] = new
            {
                skills = anthropicSkills.Select(s => new
                {
                    type = ServiceConstants.AnthropicBetaFeatures.CustomSkillType,
                    skill_id = s.SkillId,
                    version = s.Version ?? "latest"
                }).ToList()
            };
        }

        if (toolsList.Count > 0)
        {
            requestBody["tools"] = toolsList;
        }

        return requestBody;
    }

    public async Task<ChatResult?> AskMemory(Chat chat, ChatMemoryOptions memoryOptions, ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Embeddings are not supported by the Anthropic. Document reading requires embedding support.");
    }

    public async Task<string[]> GetCurrentModels()
    {
        ValidateApiKey();
        var httpClient = CreateAnthropicHttpClient();

        using var response = await httpClient.GetAsync(ModelsUrl);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response);
        }

        var json = await response.Content.ReadAsStringAsync();
        var modelResponse =
            JsonSerializer.Deserialize<AnthropicModelListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return modelResponse?.Data?.Select(m => m.Id).ToArray() ?? [];
    }

    public Task CleanSessionCache(string id)
    {
        SessionCache.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private List<ChatMessage> GetOrCreateConversation(Chat chat, bool createSession)
    {
        List<ChatMessage> conversation;
        if (createSession)
        {
            conversation = SessionCache.GetOrAdd(chat.Id, _ => new List<ChatMessage>());
        }
        else
        {
            conversation = new List<ChatMessage>();
        }

        ChatHelper.MergeMessages(conversation, chat.Messages);
        return conversation;
    }

    private void UpdateSessionCache(string chatId, string assistantResponse, bool createSession)
    {
        if (createSession && SessionCache.TryGetValue(chatId, out var messages))
        {
            messages.Add(new ChatMessage(ServiceConstants.Roles.Assistant, assistantResponse));
        }
    }

    private async Task ProcessStreamingChatAsync(
        Chat chat,
        List<ChatMessage> conversation,
        List<LLMTokenValue> tokens,
        StringBuilder resultBuilder,
        Func<LLMTokenValue, Task>? tokenCallback,
        bool interactiveUpdates,
        CancellationToken cancellationToken)
    {
        var httpClient = CreateAnthropicHttpClient(HasAnthropicSkills(chat));

        var requestBody = await BuildAnthropicRequestBody(chat, conversation, true);
        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl)
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data:"))
            {
                var data = line.Substring("data:".Length).Trim();

                try
                {
                    var chunk = JsonSerializer.Deserialize<AnthropicStreamChunk>(data,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (chunk?.Delta?.Type == "text_delta" && !string.IsNullOrEmpty(chunk.Delta.Text))
                    {
                        var token = new LLMTokenValue
                        {
                            Text = chunk.Delta.Text,
                            Type = TokenType.Message
                        };

                        tokens.Add(token);

                        if (tokenCallback != null)
                        {
                            await tokenCallback(token);
                        }

                        resultBuilder.Append(chunk.Delta.Text);

                        if (interactiveUpdates)
                        {
                            await notificationService.DispatchNotification(
                                NotificationMessageBuilder.CreateChatCompletion(chat.Id, token, false),
                                ServiceConstants.Notifications.ReceiveMessageUpdate);
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }
        }
    }

    private async Task ProcessNonStreamingChatAsync(
        Chat chat,
        List<ChatMessage> conversation,
        StringBuilder resultBuilder,
        CancellationToken cancellationToken)
    {
        var httpClient = CreateAnthropicHttpClient(HasAnthropicSkills(chat));

        var requestBody = await BuildAnthropicRequestBody(chat, conversation, false);
        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await httpClient.PostAsync(CompletionsUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse =
            JsonSerializer.Deserialize<AnthropicMessageResponse>(responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var message = chatResponse?.Content?.FirstOrDefault()?.Text;
        if (!string.IsNullOrWhiteSpace(message))
        {
            resultBuilder.Append(message);
        }
    }

    private static ChatResult CreateChatResult(Chat chat, string content, List<LLMTokenValue> tokens)
    {
        return new ChatResult
        {
            Done = true,
            CreatedAt = DateTime.UtcNow,
            Model = chat.ModelId,
            Message = new Message
            {
                Content = content,
                Tokens = tokens,
                Role = nameof(AuthorRole.Assistant),
                Type = MessageType.CloudLLM
            }.MarkProcessed()
        };
    }
}

internal class AnthropicToolUse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public object Input { get; set; } = null!;
}

internal class AnthropicToolUseBuilder
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StringBuilder InputJson { get; set; } = new();

    public AnthropicToolUse Build()
    {
        object input;
        var jsonString = InputJson.ToString().Trim();

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            input = new { };
        }
        else
        {
            try
            {
                input = JsonSerializer.Deserialize<object>(jsonString) ?? new { };
            }
            catch (JsonException)
            {
                input = new { };
            }
        }

        return new AnthropicToolUse
        {
            Id = Id,
            Name = Name,
            Input = input
        };
    }
}

file class AnthropicMessageResponse
{
    public List<AnthropicMessageContent> Content { get; set; } = [];
}

file class AnthropicMessageContent
{
    public string Type { get; set; } = default!;
    public string? Text { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public object? Input { get; set; }
}

file class AnthropicStreamChunk
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("index")]
    public int Index { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("delta")]
    public AnthropicDelta? Delta { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("content_block")]
    public AnthropicContentBlock? ContentBlock { get; set; }
}

file class AnthropicContentBlock
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("input")]
    public object? Input { get; set; }
}

file class AnthropicDelta
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string? Text { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("partial_json")]
    public string? PartialJson { get; set; }
}

file class AnthropicModelListResponse
{
    public required List<AnthropicModelInfo> Data { get; set; }
}

file class AnthropicModelInfo
{
    public required string Id { get; set; }
}
