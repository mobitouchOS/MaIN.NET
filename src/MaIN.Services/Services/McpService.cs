using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Auth;
using MaIN.Services.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using ModelContextProtocol.Client;

#pragma warning disable SKEXP0001

namespace MaIN.Services.Services;

public class McpService(MaINSettings settings, IServiceProvider serviceProvider, ILogger<McpService>? logger = null) : IMcpService
{
    // EnsureSuccessStatusCode() throws before the body is read, discarding the provider's error detail
    // (e.g. OpenAI's specific "invalid schema for function ..." message) — read first, then throw with it.
    private async Task<string> ReadResponseOrThrowAsync(HttpResponseMessage response)
    {
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger?.LogError(
                "MCP LLM request failed: {StatusCode} {ReasonPhrase} - {Body}",
                (int)response.StatusCode, response.ReasonPhrase, responseText);
            throw new HttpRequestException(
                $"MCP LLM request failed: {(int)response.StatusCode} {response.ReasonPhrase} - {responseText}",
                inner: null, response.StatusCode);
        }
        return responseText;
    }

    public async Task<McpResult> Prompt(Mcp config, List<Message> messageHistory, int? maxIterations = null)
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(
            new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Command = config.Command,
                    Arguments = config.Arguments,
                    EnvironmentVariables = config.EnvironmentVariables!
                })
        );

        var tools = await mcpClient.ListToolsAsync();
        var backendType = config.Backend ?? settings.BackendType;

        return backendType switch
        {
            BackendType.Gemini or BackendType.Vertex when maxIterations.HasValue =>
                throw new NotSupportedException($"MaxIterations is not supported for {backendType} backend."),
            BackendType.Gemini or BackendType.Vertex =>
                await PromptWithSK(mcpClient, tools, config, messageHistory, backendType),
            BackendType.Anthropic =>
                await PromptWithAnthropic(mcpClient, tools, config, messageHistory, maxIterations),
            BackendType.DeepSeek or BackendType.Ollama or BackendType.Self =>
                throw new NotSupportedException($"{backendType} does not support MCP integration."),
            _ => await PromptWithHttp(mcpClient, tools, config, messageHistory, backendType, maxIterations)
        };
    }

    // Direct HTTP loop for OpenAI-compatible backends (OpenAI, GroqCloud, xAI, Anthropic-OpenAI-compat).
    // Bypasses SK.Connectors.OpenAI 1.49.0 which has a binary incompatibility with SK.Core 1.64.0.
    private async Task<McpResult> PromptWithHttp(
        IMcpClient mcpClient,
        IList<McpClientTool> tools,
        Mcp config,
        List<Message> messageHistory,
        BackendType backendType,
        int? maxIterations = null)
    {
        var (url, apiKey) = GetEndpointAndKey(backendType, config);

        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var toolDefs = tools.Select(t => new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = t.Name,
                ["description"] = t.Description ?? "",
                ["parameters"] = t.ProtocolTool.InputSchema
            }
        }).ToList<object>();

        var messages = messageHistory
            .Select(m => (object)new Dictionary<string, object>
            {
                ["role"] = m.Role.ToLower(),
                ["content"] = m.Content
            })
            .ToList();

        var effectiveMaxIterations = maxIterations ?? 10;
        for (int i = 0; i < effectiveMaxIterations; i++)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = config.Model,
                ["messages"] = messages,
                ["tools"] = toolDefs,
                ["tool_choice"] = i == 0 ? "required" : "auto"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await client.PostAsync(url,
                new StringContent(json, Encoding.UTF8, "application/json"));

            var responseText = await ReadResponseOrThrowAsync(response);
            var responseDoc = JsonDocument.Parse(responseText);
            var message = responseDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            var hasToolCalls = message.TryGetProperty("tool_calls", out var toolCalls)
                               && toolCalls.ValueKind == JsonValueKind.Array
                               && toolCalls.GetArrayLength() > 0;

            if (!hasToolCalls)
            {
                var content = message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

                // Some models return empty content after tool use — ask for explicit summary
                if (string.IsNullOrWhiteSpace(content) && i > 0)
                {
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = "Summarize what you just did in one sentence."
                    });
                    continue;
                }

                return McpService.BuildResult(content, config.Model);
            }

            // Add assistant message with tool calls (preserve raw JSON element)
            var assistantMsg = new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = (object)(message.TryGetProperty("content", out var ac) ? ac.GetString() ?? "" : ""),
                ["tool_calls"] = toolCalls
            };
            messages.Add(assistantMsg);

            // Execute each tool via MCP client
            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                var toolName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
                var argsJson = toolCall.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                var toolCallId = toolCall.GetProperty("id").GetString()!;

                var resultText = await ExecuteToolAsync(mcpClient, toolName, argsJson);

                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCallId,
                    ["content"] = resultText
                });
            }
        }

        logger?.LogWarning("Max tool iterations ({MaxIterations}) reached. Sending final synthesis request.", effectiveMaxIterations);

        var finalRequestBody = new Dictionary<string, object>
        {
            ["model"] = config.Model,
            ["messages"] = messages,
            ["tools"] = toolDefs
        };

        var finalJson = JsonSerializer.Serialize(finalRequestBody);
        var finalResponse = await client.PostAsync(url,
            new StringContent(finalJson, Encoding.UTF8, "application/json"));
        var finalResponseText = await ReadResponseOrThrowAsync(finalResponse);
        var finalDoc = JsonDocument.Parse(finalResponseText);
        var finalMessage = finalDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");
        var finalContent = finalMessage.TryGetProperty("content", out var fc) ? fc.GetString() ?? "" : "";

        return McpService.BuildResult(finalContent, config.Model);
    }

    // Anthropic uses a different protocol: x-api-key header, input_schema instead of parameters,
    // content[] array response, tool_use/tool_result blocks instead of tool_calls.
    private async Task<McpResult> PromptWithAnthropic(
        IMcpClient mcpClient,
        IList<McpClientTool> tools,
        Mcp config,
        List<Message> messageHistory,
        int? maxIterations = null)
    {
        var apiKey = GetAnthropicKey() ?? throw new InvalidOperationException("Anthropic API key not configured.");
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var toolDefs = tools.Select(t => (object)new Dictionary<string, object?>
        {
            ["name"] = t.Name,
            ["description"] = t.Description ?? "",
            ["input_schema"] = t.ProtocolTool.InputSchema
        }).ToList();

        var systemContent = messageHistory
            .FirstOrDefault(m => m.Role.Equals("System", StringComparison.OrdinalIgnoreCase))
            ?.Content;

        var messages = messageHistory
            .Where(m => !m.Role.Equals("System", StringComparison.OrdinalIgnoreCase))
            .Select(m => (object)new Dictionary<string, object>
            {
                ["role"] = m.Role.ToLower(),
                ["content"] = m.Content
            })
            .ToList();

        var effectiveMaxIterations = maxIterations ?? 10;
        for (int i = 0; i < effectiveMaxIterations; i++)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = config.Model,
                ["max_tokens"] = 4096,
                ["messages"] = messages,
                ["tools"] = toolDefs,
                ["tool_choice"] = i == 0
                    ? (object)new Dictionary<string, object> { ["type"] = "any" }
                    : new Dictionary<string, object> { ["type"] = "auto" }
            };
            if (systemContent is not null)
            {
                requestBody["system"] = systemContent;
            }

            var json = JsonSerializer.Serialize(requestBody);
            var response = await client.PostAsync("https://api.anthropic.com/v1/messages",
                new StringContent(json, Encoding.UTF8, "application/json"));
            var responseText = await ReadResponseOrThrowAsync(response);
            var responseDoc = JsonDocument.Parse(responseText);
            var contentBlocks = responseDoc.RootElement.GetProperty("content").EnumerateArray().ToList();
            var stopReason = responseDoc.RootElement.TryGetProperty("stop_reason", out var sr)
                ? sr.GetString() : null;

            var textContent = string.Concat(contentBlocks
                .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(b => b.TryGetProperty("text", out var txt) ? txt.GetString() ?? "" : ""));

            var toolUses = contentBlocks
                .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "tool_use")
                .ToList();

            if (toolUses.Count == 0 || stopReason == "end_turn")
            {
                if (string.IsNullOrWhiteSpace(textContent) && i > 0)
                {
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "assistant",
                        ["content"] = new List<object> { new Dictionary<string, object> { ["type"] = "text", ["text"] = " " } }
                    });
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = "Summarize what you just did in one sentence."
                    });
                    continue;
                }

                return McpService.BuildResult(textContent, config.Model);
            }

            // Add assistant turn with tool_use blocks
            var assistantContent = new List<object>();
            if (!string.IsNullOrEmpty(textContent))
            {
                assistantContent.Add(new Dictionary<string, object> { ["type"] = "text", ["text"] = textContent });
            }

            foreach (var tu in toolUses)
            {
                assistantContent.Add(new Dictionary<string, object>
                {
                    ["type"] = "tool_use",
                    ["id"] = tu.GetProperty("id").GetString()!,
                    ["name"] = tu.GetProperty("name").GetString()!,
                    ["input"] = tu.GetProperty("input")
                });
            }

            messages.Add(new Dictionary<string, object> { ["role"] = "assistant", ["content"] = assistantContent });

            // Execute tools and collect tool_result blocks
            var toolResults = new List<object>();
            foreach (var tu in toolUses)
            {
                var toolName = tu.GetProperty("name").GetString()!;
                var toolId = tu.GetProperty("id").GetString()!;
                var resultText = await ExecuteToolAsync(mcpClient, toolName, tu.GetProperty("input").GetRawText());

                toolResults.Add(new Dictionary<string, object>
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolId,
                    ["content"] = resultText
                });
            }

            messages.Add(new Dictionary<string, object> { ["role"] = "user", ["content"] = toolResults });
        }

        logger?.LogWarning("Max tool iterations ({MaxIterations}) reached. Sending final synthesis request.", effectiveMaxIterations);

        var finalRequestBody = new Dictionary<string, object>
        {
            ["model"] = config.Model,
            ["max_tokens"] = 4096,
            ["messages"] = messages,
            ["tools"] = toolDefs
        };
        if (systemContent is not null)
        {
            finalRequestBody["system"] = systemContent;
        }

        var finalJson = JsonSerializer.Serialize(finalRequestBody);
        var finalResponse = await client.PostAsync("https://api.anthropic.com/v1/messages",
            new StringContent(finalJson, Encoding.UTF8, "application/json"));
        var finalResponseText = await ReadResponseOrThrowAsync(finalResponse);
        var finalDoc = JsonDocument.Parse(finalResponseText);
        var finalContent = string.Concat(finalDoc.RootElement
            .GetProperty("content")
            .EnumerateArray()
            .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
            .Select(b => b.TryGetProperty("text", out var txt) ? txt.GetString() ?? "" : ""));

        return McpService.BuildResult(finalContent, config.Model);
    }

    private (string url, string apiKey) GetEndpointAndKey(BackendType backendType, Mcp config)
    {
        return backendType switch
        {
            BackendType.OpenAi => (
                "https://api.openai.com/v1/chat/completions",
                GetOpenAiKey() ?? throw new InvalidOperationException("OpenAI API key not configured.")),
            BackendType.GroqCloud => (
                "https://api.groq.com/openai/v1/chat/completions",
                GetGroqCloudKey() ?? throw new InvalidOperationException("GroqCloud API key not configured.")),
            BackendType.Xai => (
                "https://api.x.ai/v1/chat/completions",
                GetXaiKey() ?? throw new InvalidOperationException("xAI API key not configured.")),
            _ => throw new NotSupportedException($"Backend {backendType} is not supported in MCP HTTP mode.")
        };
    }

    // SK-based path for Gemini / Vertex (Google connector 1.64.0 is version-compatible with SK.Core 1.64.0).
    private async Task<McpResult> PromptWithSK(
        IMcpClient mcpClient,
        IList<McpClientTool> tools,
        Mcp config,
        List<Message> messageHistory,
        BackendType backendType)
    {
        var builder = Kernel.CreateBuilder();
        var promptSettings = InitializeGoogleChatCompletions(builder, config, backendType);
        var kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(x => x.AsKernelFunction()));

        var chatHistory = new ChatHistory();
        foreach (var message in messageHistory)
        {
            var role = message.Role switch
            {
                nameof(AuthorRole.User) => AuthorRole.User,
                nameof(AuthorRole.Assistant) => AuthorRole.Assistant,
                nameof(AuthorRole.System) => AuthorRole.System,
                _ => AuthorRole.User
            };
            chatHistory.AddMessage(role, message.Content);
        }

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatService.GetChatMessageContentsAsync(chatHistory, promptSettings, kernel);

        return McpService.BuildResult(result.Last().Content!, config.Model);
    }

    private PromptExecutionSettings InitializeGoogleChatCompletions(IKernelBuilder kernelBuilder, Mcp config, BackendType backendType)
    {
        var model = config.Model;

        if (backendType == BackendType.Gemini)
        {
            kernelBuilder.Services.AddGoogleAIGeminiChatCompletion(
                model,
                GetGeminiKey() ?? throw new InvalidOperationException("Gemini API key not configured."));

            return new GeminiPromptExecutionSettings
            {
                ModelId = model,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
            };
        }

        // Vertex
        var auth = settings.GoogleServiceAccountAuth
                   ?? throw new InvalidOperationException("Vertex AI service account is not configured.");
        var tokenProvider = new GoogleServiceAccountTokenProvider(auth);
        var httpClient = new HttpClient();
        Func<ValueTask<string>> bearerTokenProvider = async ()
            => await tokenProvider.GetAccessTokenAsync(httpClient);

        var modelName = model.StartsWith("google/", StringComparison.OrdinalIgnoreCase)
            ? model["google/".Length..]
            : model;

        kernelBuilder.Services.AddVertexAIGeminiChatCompletion(
            modelName, bearerTokenProvider, config.Location, auth.ProjectId);

        return new GeminiPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };
    }

    private static Dictionary<string, object?> DeserializeToolArgs(string argsJson)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson)
            ?.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)(kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => (object)kvp.Value.GetString()!,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when kvp.Value.TryGetInt64(out var l) => l,
                    JsonValueKind.Number => (object)kvp.Value.GetDouble(),
                    _ => (object)kvp.Value
                }))
            ?? [];
    }

    private async Task<string> ExecuteToolAsync(IMcpClient mcpClient, string toolName, string argsJson)
    {
        var argsDict = DeserializeToolArgs(argsJson);
        var result = await mcpClient.CallToolAsync(toolName, argsDict);
        var text = string.Join("\n", result.Content.Where(c => c.Text is not null).Select(c => c.Text!));
        if (result.IsError == true)
        {
            logger?.LogError("MCP tool '{ToolName}' returned error: {Error}", toolName, text);
        }

        return text;
    }

    private static McpResult BuildResult(string content, string model) => new()
    {
        CreatedAt = DateTime.Now,
        Message = new Message
        {
            Content = content,
            Role = nameof(AuthorRole.Assistant),
            Type = MessageType.CloudLLM
        },
        Model = model
    };

    string? GetOpenAiKey()
        => settings.OpenAiKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.OpenAi.ApiKeyEnvName);
    string? GetGeminiKey()
        => settings.GeminiKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Gemini.ApiKeyEnvName);
    string? GetGroqCloudKey()
        => settings.GroqCloudKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Groq.ApiKeyEnvName);
    string? GetAnthropicKey()
        => settings.AnthropicKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Anthropic.ApiKeyEnvName);
    string? GetXaiKey()
        => settings.XaiKey ?? Environment.GetEnvironmentVariable(LLMApiRegistry.Xai.ApiKeyEnvName);
}
