using System.Text;
using System.Text.Json;
using LLama;
using LLama.Batched;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions;
using MaIN.Domain.Exceptions.Models;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.LLMService.Utils;
using MaIN.Services.Services.Models;
using MaIN.Services.Utils;
using Microsoft.KernelMemory;
using Grammar = LLama.Sampling.Grammar;
using LocalInferenceParams = MaIN.Domain.Entities.LocalInferenceParams;
#pragma warning disable KMEXP00

namespace MaIN.Services.Services.LLMService;

public class LLMService : ILLMService
{
    private const string DEFAULT_MODEL_ENV_PATH = "MaIN_ModelsPath";
    private const int MaxToolIterations = 5;

    private readonly MaINSettings options;
    private readonly INotificationService notificationService;
    private readonly IMemoryService memoryService;
    private readonly IMemoryFactory memoryFactory;
    private readonly string modelsPath;

    public LLMService(
        MaINSettings options,
        INotificationService notificationService,
        IMemoryService memoryService,
        IMemoryFactory memoryFactory)
    {
        this.options = options;
        this.notificationService = notificationService;
        this.memoryService = memoryService;
        this.memoryFactory = memoryFactory;
        modelsPath = GetModelsPath();
    }

    public async Task<ChatResult?> Send(
        Chat chat,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        chat.BackendParams ??= new LocalInferenceParams();

        if (chat.BackendParams is not LocalInferenceParams)
        {
            throw new InvalidBackendParamsException("Local LLM", nameof(LocalInferenceParams), chat.BackendParams.GetType().Name);
        }

        if (chat.Messages.Count == 0)
        {
            return null;
        }

        var lastMsg = chat.Messages.Last();
        var model = GetLocalModel(chat);

        if (model is IVisionModel)
        {
            await ChatHelper.ExtractImageFromFiles(lastMsg);
        }

        if (ChatHelper.HasFiles(lastMsg))
        {
            var memoryOptions = ChatHelper.ExtractMemoryOptions(lastMsg);

            // If tools and memory are both configured, we treat memory as a tool.
            if (chat.ToolsConfiguration?.Tools is { Count: > 0 })
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
                    return await ProcessWithToolsAsync(chat, requestOptions, cancellationToken);
                }
                finally
                {
                    chat.ToolsConfiguration = originalTools;
                    await ingestedMemory.DisposeAsync();
                }
            }

            return await AskMemory(chat, memoryOptions, requestOptions, cancellationToken);
        }

        if (chat.ToolsConfiguration?.Tools is not null && chat.ToolsConfiguration.Tools.Count != 0)
        {
            return await ProcessWithToolsAsync(chat, requestOptions, cancellationToken);
        }

        var tokens = await ProcessChatRequest(chat, model, lastMsg, requestOptions, cancellationToken);
        lastMsg.MarkProcessed();
        return await CreateChatResult(chat, tokens, requestOptions);
    }

    public Task<string[]> GetCurrentModels()
    {
        var models = Directory.GetFiles(modelsPath, "*.gguf", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(fileName => ModelRegistry.GetByFileName(fileName!) is not null)
            .Select(fileName => ModelRegistry.GetByFileName(fileName!)!.Name)
            .ToArray();

        return Task.FromResult(models);
    }

    public Task CleanSessionCache(string? id) => Task.CompletedTask;

    private async Task<IIngestedMemory> CreateIngestedMemoryAsync(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        CancellationToken ct = default)
    {
        var model = GetLocalModel(chat);
        var parameters = new ModelParams(ResolvePath(null, model.FileName))
        {
            GpuLayerCount = chat.MemoryParams.GpuLayerCount,
            ContextSize = (uint)chat.MemoryParams.ContextSize,
        };
        var disableCache = chat.Properties.CheckProperty(ServiceConstants.Properties.DisableCacheProperty);
        var llmModel = disableCache
            ? await LLamaWeights.LoadFromFileAsync(parameters, ct)
            : await ModelLoader.GetOrLoadModelAsync(modelsPath, model.FileName);

        var (km, generator, textGenerator) = memoryFactory.CreateMemoryWithModel(
            modelsPath,
            llmModel,
            model.FileName,
            chat.MemoryParams);

        await memoryService.ImportDataToMemory((km, generator), memoryOptions, ct);

        return new IngestedMemory(km, async () =>
        {
            await km.DeleteIndexAsync();

            if (disableCache)
            {
                llmModel.Dispose();
                ModelLoader.RemoveModel(model.FileName);
                textGenerator.Dispose();
            }

            generator._embedder.Dispose();
            generator._embedder._weights.Dispose();
            generator.Dispose();
        });
    }

    public async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        var userMessage = chat.Messages.Last();

        if (userMessage.Images?.Count > 0)
        {
            SearchResult searchResult;
            await using (var ingestedMemory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken))
            {
                searchResult = await ingestedMemory.Memory.SearchAsync(userMessage.Content, cancellationToken: cancellationToken);
            }

            var ctxBuilder = new StringBuilder();
            foreach (var citation in searchResult.Results.SelectMany(r => r.Partitions))
            {
                ctxBuilder.AppendLine(citation.Text);
            }

            var originalContent = userMessage.Content;
            if (ctxBuilder.Length > 0)
            {
                userMessage.Content =
                    $"Use the following context to answer the question:\n\n{ctxBuilder}\n\nQuestion: {originalContent}";
            }

            userMessage.Files = null;

            var chatResult = await Send(chat, requestOptions, cancellationToken);
            userMessage.Content = originalContent;
            return chatResult;
        }

        await using var memory = await CreateIngestedMemoryAsync(chat, memoryOptions, cancellationToken);
        var km = memory.Memory;
        var tokens = new List<LLMTokenValue>();
        MemoryAnswer result;

        if (requestOptions.InteractiveUpdates || requestOptions.TokenCallback != null)
        {
            var responseBuilder = new StringBuilder();

            var searchOptions = new SearchOptions
            {
                Stream = true
            };

            await foreach (var chunk in km.AskStreamingAsync(
                               userMessage.Content,
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

                    tokens.Add(tokenValue);

                    if (requestOptions.InteractiveUpdates)
                    {
                        await notificationService.DispatchNotification(
                            NotificationMessageBuilder.CreateChatCompletion(chat.Id, tokenValue, false),
                            ServiceConstants.Notifications.ReceiveMessageUpdate);
                    }

                    if (requestOptions.TokenCallback is not null)
                    {
                        await requestOptions.TokenCallback(tokenValue);
                    }
                }
            }

            result = new MemoryAnswer
            {
                Question = userMessage.Content,
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

            result = await km.AskAsync(
                userMessage.Content,
                options: searchOptions,
                cancellationToken: cancellationToken);
        }

        return new ChatResult
        {
            Done = true,
            CreatedAt = DateTime.Now,
            Model = chat.ModelId,
            Message = new Message
            {
                Content = memoryService.CleanResponseText(result.Result),
                Tokens = tokens,
                Role = nameof(AuthorRole.Assistant),
                Type = MessageType.LocalLLM,
            }
        };
    }

    private async Task<List<LLMTokenValue>> ProcessChatRequest(
        Chat chat,
        LocalModel model,
        Message lastMsg,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        var modelKey = model.FileName;
        var thinkingState = new ThinkingState();
        var tokens = new List<LLMTokenValue>();

        var parameters = CreateModelParameters(chat, modelKey, model.CustomPath);
        var disableCache = chat.Properties.CheckProperty(ServiceConstants.Properties.DisableCacheProperty);
        var llmModel = disableCache
            ? await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken)
            : await ModelLoader.GetOrLoadModelAsync(modelsPath, modelKey);

        var visionModel = model as IVisionModel;
        var mmProjName = visionModel?.MMProjectName
            ?? (chat.Properties.TryGetValue(ServiceConstants.Properties.MmProjNameProperty, out var p) ? p : null);

        MtmdWeights? mtmdWeights = null;
        if (mmProjName is not null && lastMsg.Image != null)
        {
            // When the model file is at a custom location (fully-qualified path),
            // look for the mmproj in the same directory. Otherwise fall back to modelsPath.
            var mmProjDir = Path.IsPathFullyQualified(modelKey)
                ? Path.GetDirectoryName(modelKey)
                : null;
            var mmProjPath = ResolvePath(mmProjDir, mmProjName);
            mtmdWeights = await MtmdWeights.LoadFromFileAsync(
                mmProjPath, llmModel, MtmdContextParams.Default(), cancellationToken);
        }

        using var executor = new BatchedExecutor(llmModel, parameters, mtmdWeights);

        var (conversation, isComplete, hasFailed) = await LLMService.InitializeConversation(
            chat, lastMsg, model, llmModel, mtmdWeights, executor, cancellationToken);

        if (!isComplete)
        {
            (tokens, isComplete, hasFailed) = await ProcessTokens(
                chat, conversation, model, llmModel, executor, thinkingState, requestOptions, cancellationToken);
        }

        if (isComplete && !hasFailed)
        {
            if (requestOptions.SaveConv)
            {
                chat.ConversationState = conversation.Save();
            }

            if (isComplete)
            {
                conversation.Dispose();
                if (disableCache)
                {
                    llmModel.Dispose();
                }
            }
        }

        mtmdWeights?.Dispose();
        return tokens;
    }

    private ModelParams CreateModelParameters(Chat chat, string modelKey, string? customPath)
    {
        return new ModelParams(ResolvePath(customPath, modelKey))
        {
            ContextSize = (uint?)chat.LocalParams!.ContextSize,
            GpuLayerCount = chat.LocalParams!.GpuLayerCount,
            SeqMax = chat.LocalParams!.SeqMax,
            BatchSize = chat.LocalParams!.BatchSize,
            UBatchSize = chat.LocalParams!.UBatchSize,
            Embeddings = chat.LocalParams!.Embeddings,
            TypeK = (GGMLType)chat.LocalParams!.TypeK,
            TypeV = (GGMLType)chat.LocalParams!.TypeV,
        };
    }

    private static async Task<(Conversation Conversation, bool IsComplete, bool HasFailed)> InitializeConversation(
        Chat chat,
        Message lastMsg,
        LocalModel model,
        LLamaWeights llmModel,
        MtmdWeights? mtmdWeights,
        BatchedExecutor executor,
        CancellationToken cancellationToken)
    {
        var isNewConversation = chat.ConversationState is null;
        var conversation = isNewConversation
            ? executor.Create()
            : executor.Load(chat.ConversationState!);

        if (lastMsg.Image != null && mtmdWeights != null)
        {
            await ProcessImageMessage(conversation, lastMsg, mtmdWeights, executor, cancellationToken);
        }
        else
        {
            ProcessTextMessage(conversation, chat, lastMsg, model, llmModel, executor, isNewConversation);
        }

        return (conversation, false, false);
    }

    private static async Task ProcessImageMessage(Conversation conversation,
        Message lastMsg,
        MtmdWeights mtmdWeights,
        BatchedExecutor executor,
        CancellationToken cancellationToken)
    {
        using var imageEmbed = mtmdWeights.LoadMedia(lastMsg.Image!);

        var mediaMarker = NativeApi.MtmdDefaultMarker() ?? "<image>";
        conversation.Prompt(
            $"USER: {mediaMarker}\n{lastMsg.Content}\nASSISTANT:",
            new ReadOnlySpan<SafeMtmdEmbed>(new[] { imageEmbed }),
            addBos: true);

        while (executor.BatchedTokenCount > 0)
        {
            await executor.Infer(cancellationToken);
        }
    }

    private static void ProcessTextMessage(Conversation conversation,
        Chat chat,
        Message lastMsg,
        LocalModel model,
        LLamaWeights llmModel,
        BatchedExecutor executor,
        bool isNewConversation)
    {
        var template = new LLamaTemplate(llmModel);
        var finalPrompt = GetFinalPrompt(lastMsg, model, isNewConversation);

        var hasTools = chat.ToolsConfiguration?.Tools is not null && chat.ToolsConfiguration.Tools.Count != 0;

        if (isNewConversation)
        {
            var messagesToProcess = hasTools
                ? chat.Messages.SkipLast(1)
                : chat.Messages.Where(x => x.Properties.ContainsKey(Message.UnprocessedMessageProperty)).SkipLast(1);

            foreach (var messageToProcess in messagesToProcess)
            {
                template.Add(messageToProcess.Role, messageToProcess.Content);
            }
        }

        if (hasTools && isNewConversation)
        {
            var toolsPrompt = FormatToolsForPrompt(chat.ToolsConfiguration!);
            finalPrompt = $"{toolsPrompt}\n\n{finalPrompt}";
        }

        template.Add(ServiceConstants.Roles.User, finalPrompt);
        template.AddAssistant = true;

        var templatedMessage = Encoding.UTF8.GetString(template.Apply());
        var tokens = isNewConversation
            ? executor.Context.Tokenize(templatedMessage, addBos: true, special: true)
            : executor.Context.Tokenize(templatedMessage);

        conversation.Prompt(tokens);
    }

    private static string FormatToolsForPrompt(ToolsConfiguration toolsConfig)
    {
        var toolsList = new StringBuilder();
        foreach (var tool in toolsConfig.Tools)
        {
            if (tool.Function == null)
            {
                continue;
            }

            toolsList.AppendLine($"- {tool.Function.Name}: {tool.Function.Description}");
            toolsList.AppendLine($"  Parameters: {JsonSerializer.Serialize(tool.Function.Parameters)}");
        }

        return $$$"""
             ## TOOLS
             You can call these tools if needed. To call a tool, respond with a JSON object inside <tool_call> tags.

             {{{toolsList}}}

             ## RESPONSE FORMAT (YOU HAVE TO CHOOSE ONE FORMAT AND CANNOT MIX THEM)##
             1. For normal conversation, just respond with plain text.
             2. For tool calls, use this format. You cannot respond with plain text before or after format. If you want to call multiple functions, you have to combine them into one array. Your response MUST contain only one tool call block:
             <tool_call>
             {"tool_calls": [{"id": "call_1", "type": "function", "function": {"name": "tool_name", "arguments": "{\"param\":\"value\"}"}},{"id": "call_2", "type": "function", "function": {"name": "tool2_name", "arguments": "{\"param1\":\"value1\",\"param2\":\"value2\"}"}}]}
             </tool_call>
             """;
    }

    private async Task<(List<LLMTokenValue> Tokens, bool IsComplete, bool HasFailed)> ProcessTokens(
        Chat chat,
        Conversation conversation,
        LocalModel model,
        LLamaWeights llmModel,
        BatchedExecutor executor,
        ThinkingState thinkingState,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        var tokens = new List<LLMTokenValue>();
        var isComplete = false;
        var hasFailed = false;

        using var sampler = LLMService.CreateSampler(chat.LocalParams!);
        var decoder = new StreamingTokenDecoder(executor.Context);

        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = chat.LocalParams!.Temperature,
                TopK = chat.LocalParams!.TopK,
                TopP = chat.LocalParams!.TopP
            },
            AntiPrompts = [llmModel.Vocab.EOT?.ToString() ?? "User:"],
            TokensKeep = chat.LocalParams!.TokensKeep,
            MaxTokens = chat.LocalParams!.MaxTokens
        };

        var maxTokens = inferenceParams.MaxTokens == -1 ? int.MaxValue : inferenceParams.MaxTokens;
        var reasoningModel = model as IReasoningModel;

        for (var i = 0; i < maxTokens && !isComplete; i++)
        {
            var decodeResult = await executor.Infer(cancellationToken);

            if (decodeResult == DecodeResult.NoKvSlot)
            {
                isComplete = true;
                hasFailed = true;
                chat.ConversationState = null;
                break;
            }

            if (decodeResult == DecodeResult.DecodeFailed)
            {
                throw new Exception("Unknown error occurred while inferring.");
            }

            if (!conversation.RequiresSampling)
            {
                continue;
            }

            var token = conversation.Sample(sampler);
            var vocab = executor.Context.NativeHandle.ModelHandle.Vocab;

            if (token.IsEndOfGeneration(vocab))
            {
                isComplete = true;
            }
            else
            {
                decoder.Add(token);
                var tokenTxt = decoder.Read();

                conversation.Prompt(token);
                var tokenValue = reasoningModel?.ReasonFunction != null
                    ? reasoningModel.ReasonFunction(tokenTxt, thinkingState)
                    : new LLMTokenValue()
                    {
                        Text = tokenTxt,
                        Type = TokenType.Message
                    };

                tokens.Add(tokenValue);

                if (requestOptions.InteractiveUpdates)
                {
                    await SendNotification(chat.Id, tokenValue, false);
                }

                requestOptions.TokenCallback?.Invoke(tokenValue);
            }
        }

        return (tokens, isComplete, hasFailed);
    }

    private static BaseSamplingPipeline CreateSampler(LocalInferenceParams inferenceParams)
    {
        return inferenceParams.Temperature == 0
            ? new GreedySamplingPipeline()
            {
                Grammar = inferenceParams.Grammar is not null
                    ? new Grammar(inferenceParams.Grammar.Value, "root")
                    : null
            }
            : new DefaultSamplingPipeline()
            {
                Temperature = inferenceParams.Temperature,
                TopP = inferenceParams.TopP,
                TopK = inferenceParams.TopK,
                Grammar = inferenceParams.Grammar is not null
                    ? new Grammar(inferenceParams.Grammar.Value, "root")
                    : null
            };
    }

    private static LocalModel GetLocalModel(Chat chat)
    {
        // Try registry lookup (TryGetById to avoid throwing for unregistered models)
        if (ModelRegistry.TryGetById(chat.ModelId, out var model) && model is LocalModel localModel)
        {
            return localModel;
        }

        // 3. Fallback: create generic local model for unregistered models
        var modelId = chat.ModelId;
        string fileName;
        if (Path.IsPathFullyQualified(modelId))
        {
            // Fully-qualified path passed as ModelId — keep as-is.
            // Applying Replace(':','-') would corrupt the drive letter: "C:\" → "C-\"
            fileName = modelId.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                ? modelId
                : modelId + ".gguf";
        }
        else
        {
            // Model name only — replace ':' with '-' (colon is illegal in Windows file names)
            fileName = modelId.Replace(':', '-');
            if (!fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".gguf";
            }
        }

        return new GenericLocalModel(FileName: fileName);
    }

    private string GetModelsPath()
    {
        var path = options.ModelsPath ?? Environment.GetEnvironmentVariable(DEFAULT_MODEL_ENV_PATH);
        return string.IsNullOrEmpty(path)
            ? throw new ModelsPathNotFoundException()
            : path;
    }

    /// <summary>
    /// Resolves the full path to a model file.
    /// If <paramref name="fileName"/> is already fully qualified (absolute), it is used as-is.
    /// Otherwise it is combined with <paramref name="customPath"/> (or <see cref="modelsPath"/>).
    /// </summary>
    private string ResolvePath(string? customPath, string fileName)
    {
        if (Path.IsPathFullyQualified(fileName))
        {
            return fileName;
        }

        return Path.Combine(customPath ?? modelsPath, fileName);
    }

    private async Task<ChatResult> CreateChatResult(Chat chat, List<LLMTokenValue> tokens,
        ChatRequestOptions requestOptions)
    {
        var responseText = string.Concat(tokens.Select(x => x.Text));

        if (requestOptions.InteractiveUpdates)
        {
            await SendNotification(chat.Id, new LLMTokenValue
            {
                Type = TokenType.FullAnswer,
                Text = responseText
            }, true);
        }

        return new ChatResult
        {
            Done = true,
            CreatedAt = DateTime.Now,
            Model = chat.ModelId,
            Message = new Message
            {
                Content = responseText,
                Tokens = tokens,
                Role = AuthorRole.Assistant.ToString(),
                Type = MessageType.LocalLLM,
            }.MarkProcessed()
        };
    }

    private async Task SendNotification(string chatId, LLMTokenValue token, bool isComplete)
    {
        await notificationService.DispatchNotification(
            NotificationMessageBuilder.CreateChatCompletion(chatId, token, isComplete),
            ServiceConstants.Notifications.ReceiveMessageUpdate);
    }

    private async Task<ChatResult> ProcessWithToolsAsync(
        Chat chat,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken)
    {
        var model = ModelRegistry.GetById(chat.ModelId);
        if (model is not LocalModel localModel)
        {
            throw new InvalidModelTypeException(nameof(LocalModel));
        }

        var iterations = 0;
        var lastResponseTokens = new List<LLMTokenValue>();
        var lastResponse = string.Empty;
        var maxToolIterations = chat.ToolsConfiguration?.MaxIterations ?? MaxToolIterations;

        while (iterations < maxToolIterations)
        {
            var lastMsg = chat.Messages.Last();
            var tokenCallbackOrg = requestOptions.TokenCallback;
            requestOptions.InteractiveUpdates = false;
            requestOptions.TokenCallback = null;
            lastResponseTokens = await ProcessChatRequest(chat, localModel, lastMsg, requestOptions, cancellationToken);
            lastMsg.MarkProcessed();
            lastResponse = string.Concat(lastResponseTokens.Select(x => x.Text));
            var responseMessage = new Message
            {
                Content = lastResponse,
                Role = AuthorRole.Assistant.ToString(),
                Type = MessageType.LocalLLM,
            };
            chat.Messages.Add(responseMessage.MarkProcessed());

            var parseResult = ToolCallParser.ParseToolCalls(lastResponse);

            // Tool not found or invalid JSON
            if (!parseResult.IsSuccess)
            {
                if (parseResult.ErrorMessage is not null) // Invalid JSON, self correction
                {
                    var errorMsg = new Message
                    {
                        Content = $"System Error: The tool call JSON was invalid. {parseResult.ErrorMessage}. Please correct the JSON format.",
                        Role = ServiceConstants.Roles.Tool,
                        Type = MessageType.LocalLLM,
                        Tool = true
                    };
                    chat.Messages.Add(errorMsg.MarkProcessed());

                    iterations++;
                    continue;
                }
                else // Final response
                {
                    requestOptions.InteractiveUpdates = true;
                    requestOptions.TokenCallback = tokenCallbackOrg;
                    await SendNotification(
                        chat.Id,
                        new LLMTokenValue
                        {
                            Type = TokenType.FullAnswer,
                            Text = lastResponse
                        },
                        false);
                    break;
                }
            }

            var toolCalls = parseResult.ToolCalls!;
            responseMessage.Properties[ServiceConstants.Properties.ToolCallsProperty] = JsonSerializer.Serialize(toolCalls);

            if (chat.Properties.CheckProperty(ServiceConstants.Properties.ClientSideToolExecutionProperty))
            {
                foreach (var toolCall in toolCalls)
                {
                    if (requestOptions.ToolCallback is not null)
                    {
                        await requestOptions.ToolCallback.Invoke(new ToolInvocation
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = false
                        });
                    }
                }

                break;
            }

            foreach (var toolCall in toolCalls)
            {
                if (chat.Properties.CheckProperty(ServiceConstants.Properties.AgentIdProperty))
                {
                    await notificationService.DispatchNotification(
                        NotificationMessageBuilder.ProcessingTools(
                            chat.Properties[ServiceConstants.Properties.AgentIdProperty],
                            string.Empty,
                            toolCall.Function.Name),
                        ServiceConstants.Notifications.ReceiveAgentUpdate);
                }

                var executor = chat.ToolsConfiguration?.GetExecutor(toolCall.Function.Name);

                if (executor is null)
                {
                    var errorMessage = $"No executor found for tool: {toolCall.Function.Name}";
                    throw new InvalidOperationException(errorMessage);
                }

                try
                {
                    if (requestOptions.ToolCallback is not null)
                    {
                        await requestOptions.ToolCallback.Invoke(new ToolInvocation
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = false
                        });
                    }

                    var toolResult = await executor(toolCall.Function.Arguments);

                    if (requestOptions.ToolCallback is not null)
                    {
                        await requestOptions.ToolCallback.Invoke(new ToolInvocation
                        {
                            ToolName = toolCall.Function.Name,
                            Arguments = toolCall.Function.Arguments,
                            Done = true
                        });
                    }

                    var toolMessage = new Message
                    {
                        Content = $"Tool result for {toolCall.Function.Name}: {toolResult}",
                        Role = ServiceConstants.Roles.Tool,
                        Type = MessageType.LocalLLM,
                        Tool = true
                    };
                    toolMessage.Properties[ServiceConstants.Properties.ToolCallIdProperty] = toolCall.Id;
                    toolMessage.Properties[ServiceConstants.Properties.ToolNameProperty] = toolCall.Function.Name;
                    chat.Messages.Add(toolMessage.MarkProcessed());
                }
                catch (Exception ex)
                {
                    var errorResult = JsonSerializer.Serialize(new { error = ex.Message });
                    var toolMessage = new Message
                    {
                        Content = $"Tool error for {toolCall.Function.Name}: {errorResult}",
                        Role = ServiceConstants.Roles.Tool,
                        Type = MessageType.LocalLLM,
                        Tool = true
                    };
                    toolMessage.Properties[ServiceConstants.Properties.ToolCallIdProperty] = toolCall.Id;
                    toolMessage.Properties[ServiceConstants.Properties.ToolNameProperty] = toolCall.Function.Name;
                    chat.Messages.Add(toolMessage.MarkProcessed());
                }
            }

            iterations++;
        }

        if (iterations >= maxToolIterations)
        {
            var errorMessage = "Maximum tool invocation iterations reached. Ending the tool-loop prematurely.";
            var iterationMessage = new Message
            {
                Content = errorMessage,
                Role = AuthorRole.System.ToString(),
                Type = MessageType.LocalLLM,
            };
            chat.Messages.Add(iterationMessage.MarkProcessed());

            await SendNotification(
                chat.Id,
                new LLMTokenValue
                {
                    Type = TokenType.FullAnswer,
                    Text = errorMessage
                },
                false);
        }

        return new ChatResult
        {
            Done = true,
            CreatedAt = DateTime.Now,
            Model = chat.ModelId,
            Message = chat.Messages.Last()
        };
    }

    private static string GetFinalPrompt(Message message, AIModel model, bool startSession)
    {
        var additionalPrompt = (model as IReasoningModel)?.AdditionalPrompt;
        return startSession && additionalPrompt != null
            ? $"{message.Content}{additionalPrompt}"
            : message.Content;
    }
}
