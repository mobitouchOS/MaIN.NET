using System.Text.Json;
using MaIN.Core.Hub;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models.Abstract;

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
        });

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
            var configuredContext = AIHub.Chat().WithModel(Utils.Model).WithMessages(messages);
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
                await HandleStreamingRequest(configuredContext, promptText, httpResponse, ct);
                return Results.Empty;
            }

            var result = await configuredContext.CompleteAsync(cancellationToken: ct);
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
        });
    }

    private static bool IsAuthorized(HttpRequest request, out OpenAiErrorResponse? error)
    {
        var requiredApiKey = Environment.GetEnvironmentVariable("MaIN__ApiKey");
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

    private static async Task HandleStreamingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        string promptText,
        HttpResponse response,
        CancellationToken ct)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        var chunkId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleSent = false;

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
    }

    private static async Task<IResult> HandleToolCallingRequest(
        MaIN.Core.Hub.Contexts.Interfaces.ChatContext.IChatConfigurationBuilder context,
        ChatCompletionRequest request,
        string promptText,
        bool isStreaming,
        HttpResponse response,
        CancellationToken ct)
    {
        var toolsBuilder = new ToolsConfigurationBuilder().WithMaxIterations(1);
        foreach (var tool in request.Tools!)
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
                execute: (string _) => Task.FromResult("{\"status\":\"pending_client_execution\"}"));
        }

        if (!string.IsNullOrEmpty(request.ToolChoice))
        {
            toolsBuilder.WithToolChoice(request.ToolChoice);
        }

        context.WithTools(toolsBuilder.Build());

        var invocations = new List<ToolInvocation>();
        var result = await context.CompleteAsync(
            toolCallback: invocation =>
            {
                if (!invocation.Done)
                {
                    invocations.Add(invocation);
                }
                return Task.CompletedTask;
            },
            cancellationToken: ct);

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
        // token-by-token streaming -- see design doc: streaming a local model's raw tool-call
        // formatting tokens would leak internal syntax into user-visible content deltas.
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
}
