using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using Utils = MaIN.InferPage.Utils;

namespace MaIN.InferPage.Services;

public sealed class AgentRunResult
{
    public required string ModelId { get; init; }
    public required string Content { get; init; }
}

public sealed class AgentRunner(IAgentService agentService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private string? SearxngBaseUrl => configuration["MaIN:SearxngBaseUrl"];

    public async Task<AgentRunResult> RunAsync(
        string agentId,
        IReadOnlyList<Message> messages,
        Func<LLMTokenValue, Task>? tokenCallback = null,
        Func<ToolInvocation, Task>? toolCallback = null,
        CancellationToken ct = default)
    {
        var chat = await agentService.GetChatByAgent(agentId);
        AgentToolsRehydrator.Rehydrate(chat, httpClientFactory, SearxngBaseUrl);

        // Stateless per call: keep the agent's own system message, replace the rest with the client's conversation.
        var system = chat.Messages.FirstOrDefault(m =>
            m.Role.Equals(ServiceConstants.Roles.System, StringComparison.OrdinalIgnoreCase));
        chat.Messages.Clear();
        if (system is not null) chat.Messages.Add(system);
        chat.Messages.AddRange(messages);

        // The service is selected by the model's backend, so build matching params; the persisted chat may carry stale/wrong-typed params.
        var backend = ModelRegistry.TryGetById(chat.ModelId, out var model) ? model!.Backend : Utils.BackendType;
        chat.BackendParams = BackendParamsFactory.Create(backend);

        var resultChat = await agentService.Process(chat, agentId, knowledge: null, translatePrompt: false,
            callbackToken: tokenCallback, callbackTool: toolCallback);

        var reply = resultChat.Messages.LastOrDefault();
        return new AgentRunResult { ModelId = resultChat.ModelId, Content = reply?.Content ?? string.Empty };
    }
}
