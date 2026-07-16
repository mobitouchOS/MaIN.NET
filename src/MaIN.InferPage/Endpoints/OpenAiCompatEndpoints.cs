using System.Text.Json;
using MaIN.Core.Hub;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.Models;
using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.InferPage.Endpoints;

public static class OpenAiCompatEndpoints
{
    public static void MapOpenAiCompatEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/models", (HttpRequest request) =>
        {
            if (!IsAuthorized(request, out var authError))
            {
                return Results.Json(authError, OpenAiJsonOptions.Options, statusCode: StatusCodes.Status401Unauthorized);
            }

            var response = new ModelListResponse
            {
                Data = string.IsNullOrEmpty(Utils.Model)
                    ? []
                    : [new ModelData { Id = Utils.Model, Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }]
            };

            return Results.Json(response, OpenAiJsonOptions.Options);
        })
        .WithName("ListModels")
        .WithTags("OpenAI-Compatible API")
        .WithSummary("Lists the model this InferPage instance is currently serving.")
        .Produces<ModelListResponse>()
        .Produces<OpenAiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/chat/completions", async (HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken ct) =>
        {
            if (!IsAuthorized(httpRequest, out var authError))
            {
                return Results.Json(authError, OpenAiJsonOptions.Options, statusCode: StatusCodes.Status401Unauthorized);
            }

            ChatCompletionRequest? request;
            try
            {
                request = await httpRequest.ReadFromJsonAsync<ChatCompletionRequest>(OpenAiJsonOptions.Options, ct);
            }
            catch (JsonException)
            {
                return BadRequest("The request body is not valid JSON.");
            }

            if (request is null || request.Messages.Count == 0)
            {
                return BadRequest("The 'messages' field is required and must not be empty.");
            }

            if (string.IsNullOrEmpty(Utils.Model))
            {
                return NotFoundModel(request.Model ?? string.Empty);
            }

            if (!string.IsNullOrEmpty(request.Model) &&
                !string.Equals(request.Model, Utils.Model, StringComparison.OrdinalIgnoreCase))
            {
                return NotFoundModel(request.Model);
            }

            var (systemPrompt, messages) = OpenAiMessageMapper.ToMainMessages(request.Messages);

            // WithModel() returns IChatMessageBuilder, which only exposes WithMessage/WithMessages;
            // WithSystemPrompt/WithInferenceParams/WithTools/CompleteAsync all live on the
            // IChatConfigurationBuilder that WithMessages(...) unlocks -- so WithMessages must be
            // called before WithSystemPrompt, even though WithSystemPrompt always inserts at index 0
            // regardless of call order.
            var configuredContext = AIHub.Chat().WithModel(Utils.Model).EnsureModelDownloaded().WithMessages(messages);
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                configuredContext.WithSystemPrompt(systemPrompt);
            }

            configuredContext.WithInferenceParams(OpenAiInferenceParamsMapper.Build(Utils.BackendType, request));

            var promptText = string.Join('\n', messages.Select(m => m.Content));
            var isStreaming = request.Stream == true;

            if (request.Tools is { Count: > 0 })
            {
                return await HandleToolCallingRequest(configuredContext, request, promptText, isStreaming, httpResponse, ct);
            }

            if (isStreaming)
            {
                return await HandleStreamingRequest(configuredContext, httpResponse, ct);
            }

            ChatResult result;
            try
            {
                result = await configuredContext.CompleteAsync(cancellationToken: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ServerError(ex);
            }

            var response = new ChatCompletionResponse
            {
                Model = Utils.Model,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new ChatCompletionResponseMessage { Role = "assistant", Content = result.Message.Content },
                        FinishReason = "stop"
                    }
                ],
                Usage = OpenAiUsageEstimator.Estimate(promptText, result.Message.Content)
            };

            return Results.Json(response, OpenAiJsonOptions.Options);
        })
        .WithName("CreateChatCompletion")
        .WithTags("OpenAI-Compatible API")
        .WithSummary("OpenAI-compatible chat completions -- supports streaming (SSE), tool calling, and response_format.")
        .Accepts<ChatCompletionRequest>("application/json")
        .Produces<ChatCompletionResponse>()
        .Produces<OpenAiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<OpenAiErrorResponse>(StatusCodes.Status401Unauthorized)
        .Produces<OpenAiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/v1/responses", async (HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken ct) =>
        {
            if (!IsAuthorized(httpRequest, out var authError))
            {
                return Results.Json(authError, OpenAiJsonOptions.Options, statusCode: StatusCodes.Status401Unauthorized);
            }

            CreateResponseRequest? request;
            try
            {
                request = await httpRequest.ReadFromJsonAsync<CreateResponseRequest>(OpenAiJsonOptions.Options, ct);
            }
            catch (JsonException ex)
            {
                return BadRequest($"Malformed JSON request body: {ex.Message}");
            }

            if (request is null)
            {
                return BadRequest("Request body cannot be null.");
            }

            if (!string.IsNullOrEmpty(request.Model) &&
                !string.Equals(request.Model, Utils.Model, StringComparison.OrdinalIgnoreCase))
            {
                return NotFoundModel(request.Model);
            }

            var (systemPrompt, messages) = OpenAiMessageMapper.ToMainMessages(request);

            var configuredContext = AIHub.Chat().WithModel(Utils.Model).EnsureModelDownloaded().WithMessages(messages);
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                configuredContext.WithSystemPrompt(systemPrompt);
            }

            configuredContext.WithInferenceParams(OpenAiInferenceParamsMapper.Build(Utils.BackendType, request));

            var promptText = string.Join('\n', messages.Select(m => m.Content));
            var isStreaming = request.Stream == true;

            if (request.Tools is { Count: > 0 })
            {
                return await HandleResponsesToolCallingRequest(configuredContext, request, promptText, isStreaming, httpResponse, ct);
            }

            if (isStreaming)
            {
                return await HandleResponsesStreamingRequest(configuredContext, promptText, httpResponse, ct);
            }

            ChatResult result;
            try
            {
                result = await configuredContext.CompleteAsync(cancellationToken: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ServerError(ex);
            }

            var response = new ResponsesResponse
            {
                Model = Utils.Model ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "completed",
                Output = [new ResponseOutputItemMessage { Role = "assistant", Content = [new ResponseOutputContentText { Text = result.Message.Content ?? string.Empty }] }],
                Usage = OpenAiUsageEstimator.Estimate(promptText, result.Message.Content ?? string.Empty)
            };

            return Results.Json(response, OpenAiJsonOptions.Options);
        })
        .WithName("CreateResponse")
        .WithTags("OpenAI-Compatible API")
        .WithSummary("OpenAI-compatible Responses API (/v1/responses) -- supports both web_search and function tools, streaming (SSE), and unified inputs.")
        .Accepts<CreateResponseRequest>("application/json")
        .Produces<ResponsesResponse>()
        .Produces<OpenAiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<OpenAiErrorResponse>(StatusCodes.Status401Unauthorized)
        .Produces<OpenAiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static bool IsAuthorized(HttpRequest request, out OpenAiErrorResponse? error)
    {
        var configuration = request.HttpContext.RequestServices.GetService<IConfiguration>();
        var requiredApiKey = configuration?["MaIN:ApiKey"] ?? Environment.GetEnvironmentVariable("MaIN__ApiKey");
        var header = request.Headers.Authorization.ToString();
        return OpenAiApiKeyAuth.IsAuthorized(string.IsNullOrEmpty(header) ? null : header, requiredApiKey, out error);
    }

    private static IResult BadRequest(string message) => Results.Json(
        new OpenAiErrorResponse { Error = new OpenAiError { Message = message, Type = "invalid_request_error" } },
        OpenAiJsonOptions.Options,
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFoundModel(string requestedModel) => Results.Json(
        new OpenAiErrorResponse
        {
            Error = new OpenAiError
            {
                Message = $"The model '{requestedModel}' does not match the model this InferPage instance is running.",
                Type = "invalid_request_error",
                Code = "model_not_found"
            }
        },
        OpenAiJsonOptions.Options,
        statusCode: StatusCodes.Status404NotFound);

    private static OpenAiErrorResponse ErrorResponse(Exception ex) => new()
    {
        Error = new OpenAiError { Message = ex.Message, Type = "server_error" }
    };

    private static IResult ServerError(Exception ex) => Results.Json(
        ErrorResponse(ex),
        OpenAiJsonOptions.Options,
        statusCode: StatusCodes.Status500InternalServerError);

    private static async Task<IResult> HandleStreamingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        HttpResponse response,
        CancellationToken ct)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        var chunkId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleSent = false;

        try
        {
            await context.CompleteAsync(
                changeOfValue: async token =>
                {
                    if (token is null || token.Type != MaIN.Domain.Models.TokenType.Message)
                    {
                        return;
                    }

                    var chunk = new ChatCompletionChunk
                    {
                        Id = chunkId,
                        Created = created,
                        Model = Utils.Model ?? string.Empty,
                        Choices =
                        [
                            new ChatCompletionChunkChoice
                            {
                                Index = 0,
                                Delta = new ChatCompletionChunkDelta
                                {
                                    Role = roleSent ? null : "assistant",
                                    Content = token.Text
                                }
                            }
                        ]
                    };
                    roleSent = true;

                    await response.WriteAsync($"data: {JsonSerializer.Serialize(chunk, OpenAiJsonOptions.Options)}\n\n", ct);
                    await response.Body.FlushAsync(ct);
                },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // response.ContentType above does not itself commit the response -- ASP.NET Core only
            // sends headers on the first body write/flush. So if generation fails before any token
            // was streamed (response.HasStarted is false), we can still return a normal
            // OpenAI-shaped JSON error with a real status code. Once at least one chunk has been
            // flushed, headers are already committed and status code can no longer change -- the
            // only OpenAI-compatible option left is a terminal SSE error frame followed by [DONE].
            if (!response.HasStarted)
            {
                return ServerError(ex);
            }

            await response.WriteAsync($"data: {JsonSerializer.Serialize(ErrorResponse(ex), OpenAiJsonOptions.Options)}\n\n", ct);
            await response.WriteAsync("data: [DONE]\n\n", ct);
            await response.Body.FlushAsync(ct);
            return Results.Empty;
        }

        var finalChunk = new ChatCompletionChunk
        {
            Id = chunkId,
            Created = created,
            Model = Utils.Model ?? string.Empty,
            Choices =
            [
                new ChatCompletionChunkChoice { Index = 0, Delta = new ChatCompletionChunkDelta(), FinishReason = "stop" }
            ]
        };
        await response.WriteAsync($"data: {JsonSerializer.Serialize(finalChunk, OpenAiJsonOptions.Options)}\n\n", ct);
        await response.WriteAsync("data: [DONE]\n\n", ct);
        await response.Body.FlushAsync(ct);
        return Results.Empty;
    }

    private static async Task<IResult> HandleToolCallingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        ChatCompletionRequest request,
        string promptText,
        bool isStreaming,
        HttpResponse response,
        CancellationToken ct)
    {
        var httpClientFactory = response.HttpContext.RequestServices.GetService<IHttpClientFactory>();
        var toolsBuilder = new ToolsConfigurationBuilder();
        bool hasServerSideTools = false;

        foreach (var tool in request.Tools!)
        {
            var toolNameOrType = tool.Function?.Name ?? tool.Type;
            if (HostedToolsResolver.TryResolveBuiltInTool(toolNameOrType, httpClientFactory, out var builtInTool))
            {
                builtInTool.Type = tool.Type;
                builtInTool.IsClientSide = false;
                toolsBuilder.AddTool(builtInTool);
                hasServerSideTools = true;
            }
            else if (tool.Function != null)
            {
                // JsonElement boxes directly as object (no re-serialization needed) -- ToolDefinition.Parameters
                // is a plain `object` re-serialized later by whichever backend builds the outbound tool schema.
                object parameters;
                if (tool.Function.Parameters.HasValue)
                {
                    parameters = tool.Function.Parameters.Value;
                }
                else
                {
                    parameters = new { type = "object", properties = new { } };
                }

                toolsBuilder.AddTool(
                    name: tool.Function.Name,
                    description: tool.Function.Description ?? string.Empty,
                    parameters: parameters,
                    execute: (string _) => Task.FromResult("{\"status\":\"pending_client_execution\"}"),
                    isClientSide: true);
            }
        }

        toolsBuilder.WithMaxIterations(hasServerSideTools ? 5 : 1);

        if (!string.IsNullOrEmpty(request.ToolChoice))
        {
            toolsBuilder.WithToolChoice(request.ToolChoice);
        }

        context.WithTools(toolsBuilder.Build()).WithClientSideToolExecution(true);

        var invocations = new List<ToolInvocation>();
        ChatResult result;
        try
        {
            result = await context.CompleteAsync(
                toolCallback: invocation =>
                {
                    if (!invocation.Done && invocation.IsClientSide)
                    {
                        invocations.Add(invocation);
                    }
                    return Task.CompletedTask;
                },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Safe to return a plain JSON error here regardless of isStreaming -- nothing has been
            // written to the response yet at this point (ContentType is only switched to
            // text/event-stream further below, once we know whether this is a tool-calls response).
            return ServerError(ex);
        }

        ChatCompletionResponseMessage message;
        string finishReason;
        List<ChatCompletionToolCallDto>? toolCallDtos = null;

        if (invocations.Count > 0)
        {
            toolCallDtos = OpenAiMessageMapper.ToToolCallDtos(invocations);
            message = new ChatCompletionResponseMessage { Role = "assistant", Content = null, ToolCalls = toolCallDtos };
            finishReason = "tool_calls";
        }
        else
        {
            message = new ChatCompletionResponseMessage { Role = "assistant", Content = result.Message.Content };
            finishReason = "stop";
        }

        if (!isStreaming)
        {
            var completionResponse = new ChatCompletionResponse
            {
                Model = Utils.Model ?? string.Empty,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = [new ChatCompletionChoice { Index = 0, Message = message, FinishReason = finishReason }],
                Usage = OpenAiUsageEstimator.Estimate(promptText, message.Content ?? string.Empty)
            };
            return Results.Json(completionResponse, OpenAiJsonOptions.Options);
        }

        // Tool-calling responses are emitted as a single terminal SSE chunk rather than true
        // token-by-token streaming: streaming a local model's raw tool-call formatting tokens
        // would leak internal syntax into user-visible content deltas.
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        var chunk = new ChatCompletionChunk
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = Utils.Model ?? string.Empty,
            Choices =
            [
                new ChatCompletionChunkChoice
                {
                    Index = 0,
                    Delta = new ChatCompletionChunkDelta { Role = "assistant", Content = message.Content, ToolCalls = toolCallDtos },
                    FinishReason = finishReason
                }
            ]
        };
        await response.WriteAsync($"data: {JsonSerializer.Serialize(chunk, OpenAiJsonOptions.Options)}\n\n", ct);
        await response.WriteAsync("data: [DONE]\n\n", ct);
        await response.Body.FlushAsync(ct);
        return Results.Empty;
    }

    private static async Task<IResult> HandleResponsesStreamingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        string promptText,
        HttpResponse response,
        CancellationToken ct)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        var responseId = $"resp_{Guid.NewGuid():N}";
        var itemId = $"msg_{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fullText = string.Empty;
        var itemAdded = false;

        await response.WriteAsync($"event: response.created\ndata: {JsonSerializer.Serialize(new { response = new { id = responseId, @object = "response", status = "in_progress", created_at = created, model = Utils.Model ?? string.Empty } }, OpenAiJsonOptions.Options)}\n\n", ct);
        await response.Body.FlushAsync(ct);

        try
        {
            await context.CompleteAsync(
                changeOfValue: async token =>
                {
                    if (token is null || token.Type != MaIN.Domain.Models.TokenType.Message)
                    {
                        return;
                    }

                    if (!itemAdded)
                    {
                        await response.WriteAsync($"event: response.output_item.added\ndata: {JsonSerializer.Serialize(new { response_id = responseId, output_index = 0, item = new ResponseOutputItemMessage { Id = itemId, Role = "assistant", Content = [] } }, OpenAiJsonOptions.Options)}\n\n", ct);
                        await response.WriteAsync($"event: response.content_part.added\ndata: {JsonSerializer.Serialize(new { response_id = responseId, item_id = itemId, output_index = 0, content_index = 0, part = new ResponseOutputContentText { Text = string.Empty } }, OpenAiJsonOptions.Options)}\n\n", ct);
                        itemAdded = true;
                    }

                    fullText += token.Text;
                    await response.WriteAsync($"event: response.output_text.delta\ndata: {JsonSerializer.Serialize(new { response_id = responseId, item_id = itemId, output_index = 0, content_index = 0, delta = token.Text }, OpenAiJsonOptions.Options)}\n\n", ct);
                    await response.Body.FlushAsync(ct);
                },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!response.HasStarted)
            {
                return ServerError(ex);
            }

            await response.WriteAsync($"event: error\ndata: {JsonSerializer.Serialize(ErrorResponse(ex), OpenAiJsonOptions.Options)}\n\n", ct);
            await response.Body.FlushAsync(ct);
            return Results.Empty;
        }

        if (!itemAdded)
        {
            await response.WriteAsync($"event: response.output_item.added\ndata: {JsonSerializer.Serialize(new { response_id = responseId, output_index = 0, item = new ResponseOutputItemMessage { Id = itemId, Role = "assistant", Content = [] } }, OpenAiJsonOptions.Options)}\n\n", ct);
            await response.WriteAsync($"event: response.content_part.added\ndata: {JsonSerializer.Serialize(new { response_id = responseId, item_id = itemId, output_index = 0, content_index = 0, part = new ResponseOutputContentText { Text = string.Empty } }, OpenAiJsonOptions.Options)}\n\n", ct);
        }

        await response.WriteAsync($"event: response.output_text.done\ndata: {JsonSerializer.Serialize(new { response_id = responseId, item_id = itemId, output_index = 0, content_index = 0, text = fullText }, OpenAiJsonOptions.Options)}\n\n", ct);
        var finalItem = new ResponseOutputItemMessage { Id = itemId, Role = "assistant", Content = [new ResponseOutputContentText { Text = fullText }] };
        await response.WriteAsync($"event: response.output_item.done\ndata: {JsonSerializer.Serialize(new { response_id = responseId, output_index = 0, item = finalItem }, OpenAiJsonOptions.Options)}\n\n", ct);

        var finalResponse = new ResponsesResponse
        {
            Id = responseId,
            Model = Utils.Model ?? string.Empty,
            CreatedAt = created,
            Status = "completed",
            Output = [finalItem],
            Usage = OpenAiUsageEstimator.Estimate(promptText, fullText)
        };
        await response.WriteAsync($"event: response.done\ndata: {JsonSerializer.Serialize(new { response = finalResponse }, OpenAiJsonOptions.Options)}\n\n", ct);
        await response.Body.FlushAsync(ct);
        return Results.Empty;
    }

    private static async Task<IResult> HandleResponsesToolCallingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        CreateResponseRequest request,
        string promptText,
        bool isStreaming,
        HttpResponse response,
        CancellationToken ct)
    {
        var httpClientFactory = response.HttpContext.RequestServices.GetService<IHttpClientFactory>();
        var toolsBuilder = new ToolsConfigurationBuilder();
        bool hasServerSideTools = false;

        foreach (var tool in request.Tools!)
        {
            var toolNameOrType = tool.Function?.Name ?? tool.Type;
            if (HostedToolsResolver.TryResolveBuiltInTool(toolNameOrType, httpClientFactory, out var builtInTool))
            {
                builtInTool.Type = tool.Type;
                builtInTool.IsClientSide = false;
                toolsBuilder.AddTool(builtInTool);
                hasServerSideTools = true;
            }
            else if (tool.Function != null)
            {
                object parameters;
                if (tool.Function.Parameters.HasValue)
                {
                    parameters = tool.Function.Parameters.Value;
                }
                else
                {
                    parameters = new { type = "object", properties = new { } };
                }

                toolsBuilder.AddTool(
                    name: tool.Function.Name,
                    description: tool.Function.Description ?? string.Empty,
                    parameters: parameters,
                    execute: (string _) => Task.FromResult("{\"status\":\"pending_client_execution\"}"),
                    isClientSide: true);
            }
        }

        toolsBuilder.WithMaxIterations(hasServerSideTools ? 5 : 1);

        if (!string.IsNullOrEmpty(request.ToolChoice))
        {
            toolsBuilder.WithToolChoice(request.ToolChoice);
        }

        context.WithTools(toolsBuilder.Build()).WithClientSideToolExecution(true);

        var invocations = new List<ToolInvocation>();
        ChatResult result;
        try
        {
            result = await context.CompleteAsync(
                toolCallback: invocation =>
                {
                    if (!invocation.Done && invocation.IsClientSide)
                    {
                        invocations.Add(invocation);
                    }
                    return Task.CompletedTask;
                },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ServerError(ex);
        }

        List<object> outputItems;
        string contentText = result.Message.Content ?? string.Empty;

        if (invocations.Count > 0)
        {
            outputItems = OpenAiMessageMapper.ToResponseOutputItems(invocations);
        }
        else
        {
            outputItems = [new ResponseOutputItemMessage { Role = "assistant", Content = [new ResponseOutputContentText { Text = contentText }] }];
        }

        var completionResponse = new ResponsesResponse
        {
            Id = $"resp_{Guid.NewGuid():N}",
            Model = Utils.Model ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "completed",
            Output = outputItems,
            Usage = OpenAiUsageEstimator.Estimate(promptText, contentText)
        };

        if (!isStreaming)
        {
            return Results.Json(completionResponse, OpenAiJsonOptions.Options);
        }

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        await response.WriteAsync($"event: response.created\ndata: {JsonSerializer.Serialize(new { response = new { id = completionResponse.Id, @object = "response", status = "in_progress", created_at = completionResponse.CreatedAt, model = completionResponse.Model } }, OpenAiJsonOptions.Options)}\n\n", ct);

        for (int i = 0; i < outputItems.Count; i++)
        {
            var item = outputItems[i];
            await response.WriteAsync($"event: response.output_item.added\ndata: {JsonSerializer.Serialize(new { response_id = completionResponse.Id, output_index = i, item }, OpenAiJsonOptions.Options)}\n\n", ct);
            if (item is ResponseOutputItemMessage msgItem && msgItem.Content.Count > 0)
            {
                await response.WriteAsync($"event: response.content_part.added\ndata: {JsonSerializer.Serialize(new { response_id = completionResponse.Id, item_id = msgItem.Id, output_index = i, content_index = 0, part = msgItem.Content[0] }, OpenAiJsonOptions.Options)}\n\n", ct);
                await response.WriteAsync($"event: response.output_text.done\ndata: {JsonSerializer.Serialize(new { response_id = completionResponse.Id, item_id = msgItem.Id, output_index = i, content_index = 0, text = msgItem.Content[0].Text }, OpenAiJsonOptions.Options)}\n\n", ct);
            }
            await response.WriteAsync($"event: response.output_item.done\ndata: {JsonSerializer.Serialize(new { response_id = completionResponse.Id, output_index = i, item }, OpenAiJsonOptions.Options)}\n\n", ct);
        }

        await response.WriteAsync($"event: response.done\ndata: {JsonSerializer.Serialize(new { response = completionResponse }, OpenAiJsonOptions.Options)}\n\n", ct);
        await response.Body.FlushAsync(ct);
        return Results.Empty;
    }
}
