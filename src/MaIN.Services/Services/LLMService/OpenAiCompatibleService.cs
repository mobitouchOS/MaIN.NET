using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLama.Common;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Models;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.LLMService.Utils;
using MaIN.Services.Services.Models;
using MaIN.Services.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace MaIN.Services.Services.LLMService;

public abstract class OpenAiCompatibleService(
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<OpenAiCompatibleService>? logger = null)
    : ILLMService
{
    protected readonly INotificationService _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    protected readonly IMemoryFactory _memoryFactory = memoryFactory ?? throw new ArgumentNullException(nameof(memoryFactory));
    protected readonly IMemoryService _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));

    private static readonly ConcurrentDictionary<string, List<ChatMessage>> SessionCache = new();
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif"];

    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    protected abstract string GetApiKey();
    protected abstract string GetApiName();
    protected abstract void ValidateApiKey();
    protected abstract Type ExpectedParamsType { get; }
    protected virtual string HttpClientName => ServiceConstants.HttpClients.OpenAiClient;
    protected virtual string ChatCompletionsUrl => ServiceConstants.ApiUrls.OpenAiChatCompletions;
    protected virtual string ModelsUrl => ServiceConstants.ApiUrls.OpenAiModels;
    protected virtual int MaxToolIterations => 5;
    protected virtual bool SupportsSemanticSearch => true;

    public virtual async Task<ChatResult?> Send(
        Chat chat,
        ChatRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        if (chat.BackendParams is not null && chat.BackendParams?.GetType() != ExpectedParamsType)
        {
            throw new InvalidBackendParamsException(GetApiName(), ExpectedParamsType.Name, chat.BackendParams!.GetType().Name);
        }

        ValidateApiKey();
        if (!chat.Messages.Any())
        {
            return null;
        }

        List<LLMTokenValue> tokens = new();
        string apiKey = GetApiKey();

        var lastMessage = chat.Messages.Last();
        await ExtractImageFromFiles(lastMessage);

        List<ChatMessage> conversation = GetOrCreateConversation(chat, options.CreateSession);
        StringBuilder resultBuilder = new();
        if (HasFiles(lastMessage))
        {
            var memoryOptions = ChatHelper.ExtractMemoryOptions(lastMessage);

            // If tools and memory are both configured, we treat memory as a tool.
            if (SupportsSemanticSearch && chat.ToolsConfiguration?.Tools is { Count: > 0 })
            {
                var ingestedMemory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken);
                var originalTools = chat.ToolsConfiguration;
                try
                {
                    chat.ToolsConfiguration = new ToolsConfiguration
                    {
                        Tools = [.. originalTools.Tools, FileSearchTool.Create(ingestedMemory, cancellationToken)],
                        ToolChoice = originalTools.ToolChoice,
                        MaxIterations = originalTools.MaxIterations
                    };
                    return await ProcessWithToolsAsync(chat, conversation, apiKey, tokens, options, cancellationToken);
                }
                finally
                {
                    chat.ToolsConfiguration = originalTools;
                    await ingestedMemory.DisposeAsync();
                }
            }

            var result = memoryOptions;
            var memoryResult = await AskMemory(chat, result, options, cancellationToken);
            resultBuilder.Append(memoryResult!.Message.Content);
            lastMessage.MarkProcessed();
            UpdateSessionCache(chat.Id, resultBuilder.ToString(), options.CreateSession);
            return CreateChatResult(chat, resultBuilder.ToString(), memoryResult.Message.Tokens);
        }

        if (chat.ToolsConfiguration?.Tools is not null && chat.ToolsConfiguration.Tools.Any())
        {
            return await ProcessWithToolsAsync(
                chat,
                conversation,
                apiKey,
                tokens,
                options,
                cancellationToken);
        }

        if (options.InteractiveUpdates || options.TokenCallback is not null)
        {
            await ProcessStreamingChatAsync(
                chat,
                conversation,
                apiKey,
                tokens,
                resultBuilder,
                options,
                cancellationToken);
        }
        else
        {
            await ProcessNonStreamingChatAsync(
                chat,
                conversation,
                apiKey,
                resultBuilder,
                options,
                cancellationToken);
        }

        var finalToken = new LLMTokenValue { Text = resultBuilder.ToString(), Type = TokenType.FullAnswer };
        tokens.Add(finalToken);

        if (options.InteractiveUpdates)
        {
            await _notificationService.DispatchNotification(
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
        string apiKey,
        List<LLMTokenValue> tokens,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        StringBuilder resultBuilder = new();
        StringBuilder fullResponseBuilder = new();
        int iterations = 0;
        var maxToolIterations = chat.ToolsConfiguration?.MaxIterations ?? MaxToolIterations;

        while (iterations < maxToolIterations)
        {
            if (iterations > 0 && options.InteractiveUpdates && fullResponseBuilder.Length > 0)
            {
                var spaceToken = new LLMTokenValue { Text = " ", Type = TokenType.Message };
                tokens.Add(spaceToken);

                if (options.TokenCallback is not null)
                {
                    await options.TokenCallback(spaceToken);
                }

                await _notificationService.DispatchNotification(
                    NotificationMessageBuilder.CreateChatCompletion(chat.Id, spaceToken, false),
                    ServiceConstants.Notifications.ReceiveMessageUpdate);
            }

            List<ToolCall>? currentToolCalls;
            if (options.InteractiveUpdates || options.TokenCallback is not null)
            {
                currentToolCalls = await ProcessStreamingChatWithToolsAsync(
                    chat,
                    conversation,
                    apiKey,
                    tokens,
                    resultBuilder,
                    options,
                    cancellationToken);
            }
            else
            {
                currentToolCalls = await ProcessNonStreamingChatWithToolsAsync(
                    chat,
                    conversation,
                    apiKey,
                    resultBuilder,
                    options,
                    cancellationToken);
            }

            if (resultBuilder.Length > 0)
            {
                if (fullResponseBuilder.Length > 0)
                {
                    fullResponseBuilder.Append(' ');
                }

                fullResponseBuilder.Append(resultBuilder);
            }

            if (currentToolCalls is null || currentToolCalls.Count == 0)
            {
                break;
            }

            conversation.Add(new ChatMessage(ServiceConstants.Roles.Assistant, resultBuilder.ToString())
            {
                ToolCalls = currentToolCalls
            });

            if (chat.Properties.CheckProperty(ServiceConstants.Properties.ClientSideToolExecutionProperty) &&
                currentToolCalls.Any(tc => chat.ToolsConfiguration?.GetDefinition(tc.Function.Name)?.IsClientSide != false))
            {
                foreach (var toolCall in currentToolCalls.Where(tc => chat.ToolsConfiguration?.GetDefinition(tc.Function.Name)?.IsClientSide != false))
                {
                    if (options.ToolCallback is not null)
                    {
                        await options.ToolCallback.Invoke(new ToolInvocation()
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = false,
                            IsClientSide = true
                        });
                    }
                }

                break;
            }

            foreach (var toolCall in currentToolCalls)
            {
                if (chat.Properties.CheckProperty(ServiceConstants.Properties.AgentIdProperty))
                {
                    await _notificationService.DispatchNotification(
                        NotificationMessageBuilder.ProcessingTools(chat.Properties[ServiceConstants.Properties.AgentIdProperty],
                            string.Empty,
                            toolCall.Function.Name),
                        ServiceConstants.Notifications.ReceiveAgentUpdate);
                }

                var executor = chat.ToolsConfiguration?.GetExecutor(toolCall.Function.Name);

                if (executor is null)
                {
                    var errorMessage = $"No executor found for tool: {toolCall.Function.Name}";
                    logger?.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                try
                {
                    if (options.ToolCallback is not null)
                    {
                        await options.ToolCallback.Invoke(new ToolInvocation()
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = false
                        });
                    }
                    var toolResult = await executor(toolCall.Function.Arguments);
                    if (options.ToolCallback is not null)
                    {
                        await options.ToolCallback.Invoke(new ToolInvocation()
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = true
                        });
                    }
                    var toolMessage = new ChatMessage(ServiceConstants.Roles.Tool, toolResult)
                    {
                        ToolCallId = toolCall.Id,
                        Name = toolCall.Function.Name
                    };
                    conversation.Add(toolMessage);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error executing tool {ToolName}", toolCall.Function.Name);

                    var errorResult = JsonSerializer.Serialize(new { error = ex.Message });
                    var toolMessage = new ChatMessage(ServiceConstants.Roles.Tool, errorResult)
                    {
                        ToolCallId = toolCall.Id,
                        Name = toolCall.Function.Name
                    };
                    conversation.Add(toolMessage);
                }
            }

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
            await _notificationService.DispatchNotification(
                NotificationMessageBuilder.CreateChatCompletion(chat.Id, finalToken, true),
                ServiceConstants.Notifications.ReceiveMessageUpdate);
        }

        chat.Messages.Last().MarkProcessed();
        UpdateSessionCache(chat.Id, finalResponse, options.CreateSession);
        return CreateChatResult(chat, finalResponse, tokens);
    }

    private async Task<List<ToolCall>?> ProcessStreamingChatWithToolsAsync(
        Chat chat,
        List<ChatMessage> conversation,
        string apiKey,
        List<LLMTokenValue> tokens,
        StringBuilder resultBuilder,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        SetAuthorizationIfNeeded(client, apiKey);

        var requestBody = BuildRequestBody(chat, conversation, true);

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, MediaTypeNames.Application.Json);

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
        {
            Content = content
        };

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var toolCallsBuilder = new Dictionary<int, ToolCallBuilder>();

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

            if (line.StartsWith("data: "))
            {
                var data = line.Substring("data: ".Length).Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                try
                {
                    var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var choice = chunk?.Choices?.FirstOrDefault();

                    if (choice?.Delta is not null)
                    {
                        // Handle content
                        if (!string.IsNullOrEmpty(choice.Delta.Content))
                        {
                            var token = new LLMTokenValue
                            {
                                Text = choice.Delta.Content,
                                Type = TokenType.Message
                            };
                            tokens.Add(token);
                            resultBuilder.Append(choice.Delta.Content);

                            await InvokeTokenCallbackAsync(options.TokenCallback, token);

                            if (options.InteractiveUpdates)
                            {
                                await _notificationService.DispatchNotification(
                                    NotificationMessageBuilder.CreateChatCompletion(chat.Id, token, false),
                                    ServiceConstants.Notifications.ReceiveMessageUpdate);
                            }
                        }

                        if (choice.Delta.ToolCalls is not null)
                        {
                            foreach (var toolCallChunk in choice.Delta.ToolCalls)
                            {
                                if (!toolCallsBuilder.ContainsKey(toolCallChunk.Index))
                                {
                                    toolCallsBuilder[toolCallChunk.Index] = new ToolCallBuilder();
                                }

                                var builder = toolCallsBuilder[toolCallChunk.Index];

                                if (!string.IsNullOrEmpty(toolCallChunk.Id))
                                {
                                    builder.Id = toolCallChunk.Id;
                                }

                                if (!string.IsNullOrEmpty(toolCallChunk.Type))
                                {
                                    builder.Type = toolCallChunk.Type;
                                }

                                if (toolCallChunk.ExtraContent.HasValue)
                                {
                                    builder.ExtraContent = toolCallChunk.ExtraContent;
                                }

                                if (toolCallChunk.Function is not null)
                                {
                                    if (!string.IsNullOrEmpty(toolCallChunk.Function.Name))
                                    {
                                        builder.FunctionName = toolCallChunk.Function.Name;
                                    }

                                    if (!string.IsNullOrEmpty(toolCallChunk.Function.Arguments))
                                    {
                                        builder.FunctionArguments.Append(toolCallChunk.Function.Arguments);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    logger?.LogError(ex, "Failed to parse chunk: {Data}", data);
                }
            }
        }

        // Build final tool calls from accumulated chunks
        if (toolCallsBuilder.Count != 0)
        {
            return [.. toolCallsBuilder.Values.Select(b => b.Build())];
        }

        return null;
    }

    private async Task<List<ToolCall>?> ProcessNonStreamingChatWithToolsAsync(
        Chat chat,
        List<ChatMessage> conversation,
        string apiKey,
        StringBuilder resultBuilder,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        SetAuthorizationIfNeeded(client, apiKey);

        var requestBody = BuildRequestBody(chat, conversation, false);

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, MediaTypeNames.Application.Json);

        using var response = await client.PostAsync(ChatCompletionsUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(
            responseJson, DefaultJsonSerializerOptions);

        var message = chatResponse?.Choices?.FirstOrDefault()?.Message;

        if (message?.Content is not null)
        {
            resultBuilder.Append(message.Content);
        }

        return message?.ToolCalls;
    }

    // private protected is because IIngestedMemory is internal. This keyword allows the method to be overridden in
    // derived classes within the same assembly.
    private protected virtual async Task<IIngestedMemory> CreateIngestedMemoryAsync(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        CancellationToken ct = default)
    {
        var kernel = _memoryFactory.CreateMemoryWithOpenAi(GetApiKey(), chat.MemoryParams);
        await _memoryService.ImportDataToMemory((kernel, null), memoryOptions, ct);
        return new IngestedMemory(kernel, () => kernel.DeleteIndexAsync());
    }

    public virtual async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        if (chat.Messages.Count == 0)
        {
            return null;
        }

        var lastMessage = chat.Messages.Last();
        var userQuery = lastMessage.Content;

        if (chat.MemoryParams.Grammar is not null)
        {
            var jsonGrammarConverter = new GrammarToJsonConverter();
            var jsonGrammar = jsonGrammarConverter.ConvertToJson(chat.MemoryParams.Grammar);
            userQuery = $"{userQuery} | Respond only using the following JSON format: \n{jsonGrammar}\n. Do not add explanations, code tags, or any extra content.";
        }

        // If there are images, use SearchAsync + regular chat with images
        if (ChatHelper.HasImages(lastMessage))
        {
            SearchResult searchResult;
            await using (var imagesMemory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken))
            {
                searchResult = await imagesMemory.Memory.SearchAsync(userQuery, cancellationToken: cancellationToken);
            }

            // Build context from search results
            var contextBuilder = new StringBuilder();
            foreach (var citation in searchResult.Results.SelectMany(r => r.Partitions))
            {
                contextBuilder.AppendLine(citation.Text);
            }

            // Create a temporary message with context + original query
            var contextEnhancedContent = string.IsNullOrEmpty(contextBuilder.ToString())
                ? userQuery
                : $"Use the following context to answer the question:\n\n{contextBuilder}\n\nQuestion: {userQuery}";

            // Create conversation with context-enhanced message
            var conversation = new List<ChatMessage>
            {
                new(ServiceConstants.Roles.User, contextEnhancedContent)
                {
                    OriginalMessage = new Message
                    {
                        Role = "User",
                        Content = contextEnhancedContent,
                        Type = MessageType.CloudLLM,
                        Images = lastMessage.Images
                    }
                }
            };

            var tokens = new List<LLMTokenValue>();
            var resultBuilder = new StringBuilder();

            if (requestOptions.InteractiveUpdates || requestOptions.TokenCallback is not null)
            {
                await ProcessStreamingChatAsync(chat, conversation, GetApiKey(), tokens, resultBuilder, requestOptions, cancellationToken);
            }
            else
            {
                await ProcessNonStreamingChatAsync(chat, conversation, GetApiKey(), resultBuilder, requestOptions, cancellationToken);
            }

            var finalToken = new LLMTokenValue { Text = resultBuilder.ToString(), Type = TokenType.FullAnswer };
            tokens.Add(finalToken);

            if (requestOptions.InteractiveUpdates)
            {
                await _notificationService.DispatchNotification(
                    NotificationMessageBuilder.CreateChatCompletion(chat.Id, finalToken, true),
                    ServiceConstants.Notifications.ReceiveMessageUpdate);
            }

            return CreateChatResult(chat, resultBuilder.ToString(), tokens);
        }

        // No images - use standard AskAsync flow
        await using var ingestedMemory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken);
        var kernel = ingestedMemory.Memory;
        MemoryAnswer retrievedContext;
        var standardTokens = new List<LLMTokenValue>();

        if (requestOptions.InteractiveUpdates || requestOptions.TokenCallback is not null)
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

                    standardTokens.Add(tokenValue);

                    if (requestOptions.InteractiveUpdates)
                    {
                        await notificationService.DispatchNotification(
                            NotificationMessageBuilder.CreateChatCompletion(chat.Id, tokenValue, false),
                            ServiceConstants.Notifications.ReceiveMessageUpdate);
                    }

                    await InvokeTokenCallbackAsync(requestOptions.TokenCallback, tokenValue);
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

            // KM's default prompt template emits the literal "INFO NOT FOUND" string when the
            // answer-generation model decides it can't answer the question from retrieved chunks.
            // That's fine for strict QA but breaks creative use (e.g. "synthesise a newsletter
            // from these news items"). When retrieval did surface chunks, fall back to returning
            // them raw so the downstream step (which carries the real skill instructions) can do
            // its own synthesis.
            if (retrievedContext.NoResult)
            {
                var searchProbe = await kernel.SearchAsync(userQuery, cancellationToken: cancellationToken);
                var retrievedChunks = searchProbe.Results.SelectMany(r => r.Partitions).ToList();

                if (retrievedChunks.Count > 0)
                {
                    var contextBuilder = new StringBuilder();
                    foreach (var partition in retrievedChunks.OrderByDescending(p => p.Relevance))
                    {
                        if (string.IsNullOrWhiteSpace(partition.Text))
                        {
                            continue;
                        }

                        contextBuilder.AppendLine(partition.Text);
                        contextBuilder.AppendLine();
                    }

                    var fallback = contextBuilder.ToString().TrimEnd();

                    logger?.LogInformation(
                        "AskMemory: KernelMemory returned INFO NOT FOUND but {Count} relevant chunk(s) were retrieved; falling back to raw context ({Len} chars).",
                        retrievedChunks.Count, fallback.Length);

                    retrievedContext = new MemoryAnswer
                    {
                        Question = userQuery,
                        Result = fallback,
                        NoResult = false
                    };
                }
            }
        }

        return CreateChatResult(chat, retrievedContext.Result, standardTokens);
    }

    public virtual async Task<string[]> GetCurrentModels()
    {
        ValidateApiKey();

        var client = _httpClientFactory.CreateClient(HttpClientName);
        SetAuthorizationIfNeeded(client, GetApiKey());

        using var response = await client.GetAsync(ModelsUrl);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response);
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var modelsResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(responseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return (modelsResponse?.Data?
                    .Select(m => m.Id)
                    .Where(id => id is not null)
                    .ToArray()
                ?? [])!;
    }

    public Task CleanSessionCache(string id)
    {
        SessionCache.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    protected List<ChatMessage> GetOrCreateConversation(Chat chat, bool createSession)
    {
        List<ChatMessage> conversation;
        if (createSession)
        {
            conversation = SessionCache.GetOrAdd(chat.Id, new List<ChatMessage>());
        }
        else
        {
            conversation = new List<ChatMessage>();
        }

        ChatHelper.MergeMessages(conversation, chat.Messages);
        return conversation;
    }

    protected void UpdateSessionCache(string chatId, string assistantResponse, bool createSession)
    {
        if (createSession && SessionCache.TryGetValue(chatId, out var history))
        {
            history.Add(new ChatMessage(ServiceConstants.Roles.Assistant, assistantResponse));
        }
    }

    protected static async Task ExtractImageFromFiles(Message message)
    {
        if (message.Files is null || message.Files.Count == 0)
        {
            return;
        }

        var imageFiles = message.Files
            .Where(f => ImageExtensions.Contains(f.Extension.ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var imageBytesList = new List<byte[]>();
        foreach (var imageFile in imageFiles)
        {
            if (imageFile.StreamContent is not null)
            {
                using var ms = new MemoryStream();
                imageFile.StreamContent.Position = 0;
                await imageFile.StreamContent.CopyToAsync(ms);
                imageBytesList.Add(ms.ToArray());
            }
            else if (imageFile.Path is not null)
            {
                imageBytesList.Add(await File.ReadAllBytesAsync(imageFile.Path));
            }

            message.Files.Remove(imageFile);
        }

        message.Images = imageBytesList;

        if (message.Files.Count == 0)
        {
            message.Files = null;
        }
    }

    protected static bool HasFiles(Message message)
    {
        return message.Files is not null && message.Files.Count > 0;
    }

    private static void SetAuthorizationIfNeeded(HttpClient client, string apiKey)
    {
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
    private async Task ProcessStreamingChatAsync(
        Chat chat,
        List<ChatMessage> conversation,
        string apiKey,
        List<LLMTokenValue> tokens,
        StringBuilder resultBuilder,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        SetAuthorizationIfNeeded(client, apiKey);

        var requestBody = BuildRequestBody(chat, conversation, true);

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, MediaTypeNames.Application.Json);

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
        {
            Content = content
        };

        using var response = await client.SendAsync(
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
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data: "))
            {
                var data = line.Substring("data: ".Length).Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                try
                {
                    var token = ProcessChatCompletionChunk(data);

                    if (token is not null)
                    {
                        tokens.Add(token);

                        await InvokeTokenCallbackAsync(options.TokenCallback, token);
                        if (token.Type == TokenType.Message)
                        {
                            resultBuilder.Append(token.Text);
                        }

                        if (options.InteractiveUpdates)
                        {
                            await _notificationService.DispatchNotification(
                                NotificationMessageBuilder.CreateChatCompletion(chat.Id, token, false),
                                ServiceConstants.Notifications.ReceiveMessageUpdate);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    logger?.LogError(ex, "Failed to parse chunk: {Data}", data);
                }
            }
        }
    }

    private async Task HandleApiError(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var errorResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var errorMessage = ExtractApiErrorMessage(errorResponseBody);

        throw new LLMApiException(GetApiName(), response.StatusCode, errorMessage ?? errorResponseBody);
    }

    private static string? ExtractApiErrorMessage(string json)
    {
        try
        {
            using var jasonDocument = JsonDocument.Parse(json);

            if (jasonDocument.RootElement.ValueKind == JsonValueKind.Array)
            {
                var firstElement = jasonDocument.RootElement[0];
                if (firstElement.TryGetProperty("error", out var arrayError) &&
                    arrayError.TryGetProperty("message", out var arrayMessage))
                {
                    return arrayMessage.GetString();
                }
            }
            else if (jasonDocument.RootElement.TryGetProperty("error", out var error) &&
                     error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // If the response is not a valid JSON or doesn't match the expected schema,
            // we fall back to the raw response body in the calling method.
            return null;
        }

        return null;
    }

    protected virtual LLMTokenValue? ProcessChatCompletionChunk(string data)
    {
        var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var value = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;

        return string.IsNullOrEmpty(value)
            ? null
            : new LLMTokenValue { Text = value, Type = TokenType.Message };
    }

    private async Task ProcessNonStreamingChatAsync(
        Chat chat,
        List<ChatMessage> conversation,
        string apiKey,
        StringBuilder resultBuilder,
        ChatRequestOptions options,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        SetAuthorizationIfNeeded(client, apiKey);

        var requestBody = BuildRequestBody(chat, conversation, false);

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, MediaTypeNames.Application.Json);

        using var response = await client.PostAsync(ChatCompletionsUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleApiError(response, cancellationToken);
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse =
            JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, DefaultJsonSerializerOptions);
        var responseContent = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (responseContent is not null)
        {
            resultBuilder.Append(responseContent);
        }
    }

    private object BuildRequestBody(Chat chat, List<ChatMessage> conversation, bool stream)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = chat.ModelId,
            ["messages"] = ChatHelper.BuildMessagesArray(conversation, chat, ImageType.AsUrl).Result,
            ["stream"] = stream
        };

        ApplyBackendParams(requestBody, chat);
        ApplyAdditionalParams(requestBody, chat);

        if (chat.ToolsConfiguration?.Tools is not null && chat.ToolsConfiguration.Tools.Any())
        {
            requestBody["tools"] = chat.ToolsConfiguration.Tools.Select(t => new
            {
                type = t.Type,
                function = t.Function is not null ? new
                {
                    name = t.Function.Name,
                    description = t.Function.Description,
                    parameters = t.Function.Parameters
                } : null
            }).ToList();

            if (!string.IsNullOrEmpty(chat.ToolsConfiguration.ToolChoice))
            {
                requestBody["tool_choice"] = chat.ToolsConfiguration.ToolChoice;
            }
        }

        return requestBody;
    }

    protected virtual void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
    }

    private static void ApplyAdditionalParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams?.AdditionalParams is null)
        {
            return;
        }

        foreach (var (key, value) in chat.BackendParams.AdditionalParams)
        {
            requestBody[key] = value;
        }
    }

    protected static ChatResult CreateChatResult(Chat chat, string content, List<LLMTokenValue> tokens)
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
                Role = AuthorRole.Assistant.ToString(),
                Type = MessageType.LocalLLM
            }.MarkProcessed()
        };
    }

    private static async Task InvokeTokenCallbackAsync(Func<LLMTokenValue, Task>? callback, LLMTokenValue token)
    {
        if (callback is not null)
        {
            await callback.Invoke(token);
        }
    }
}

public class ChatMessage
{
    public string Role { get; set; }
    public object Content { get; set; }
    public Message? OriginalMessage { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }

    public ChatMessage(string role, object content)
    {
        Role = role;
        Content = content;
    }
}

// Helper class for building tool calls from streaming chunks
internal class ToolCallBuilder
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public string FunctionName { get; set; } = string.Empty;
    public StringBuilder FunctionArguments { get; set; } = new();
    public JsonElement? ExtraContent { get; set; }

    public ToolCall Build()
    {
        return new ToolCall
        {
            Id = Id,
            Type = Type,
            Function = new FunctionCall
            {
                Name = FunctionName,
                Arguments = FunctionArguments.ToString()
            },
            ExtraContent = ExtraContent
        };
    }
}

internal enum ImageType
{
    AsUrl,
    AsBase64
}

file class ChatCompletionResponse
{
    public List<Choice>? Choices { get; set; }
}

file class Choice
{
    public ChatMessageResponse? Message { get; set; }
}

file class ChatMessageResponse
{
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}

file class ChatCompletionChunk
{
    public List<ChoiceChunk>? Choices { get; set; }
}

file class ChoiceChunk
{
    public Delta? Delta { get; set; }
}

file class Delta
{
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCallChunk>? ToolCalls { get; set; }
}

file class ToolCallChunk
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public string? Type { get; set; }
    public FunctionCallChunk? Function { get; set; }

    [JsonPropertyName("extra_content")]
    public JsonElement? ExtraContent { get; set; }
}

file class FunctionCallChunk
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

file class OpenAiModelsResponse
{
    public List<OpenAiModel>? Data { get; set; }
}

file class OpenAiModel
{
    public string? Id { get; set; }
}
