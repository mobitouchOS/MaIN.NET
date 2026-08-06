using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Repositories;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using Utils = MaIN.InferPage.Utils;

namespace MaIN.InferPage.Services;

public sealed class AgentRunResult
{
    public required string ModelId { get; init; }
    public required string Content { get; init; }
}

// AgentRunner is a singleton, so concurrent stateless calls for the same agent must never share or
// mutate the agent's persisted Chat: the step pipeline writes whatever Chat instance it's given back
// to the repository on every step (see StepProcessor.ProcessSteps -> updateChat), so operating on the
// real chat.Id would race concurrent callers against each other and permanently overwrite the agent's
// real session with the client's one-shot conversation. Instead we run a deep-cloned copy under a
// transient id, registered just long enough for the pipeline's mid-run writes to have somewhere to land.
public sealed class AgentRunner(
    IAgentService agentService,
    IChatRepository chatRepository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private string? SearxngBaseUrl => configuration["MaIN:SearxngBaseUrl"];

    public async Task<AgentRunResult> RunAsync(
        string agentId,
        IReadOnlyList<Message> messages,
        Func<LLMTokenValue, Task>? tokenCallback = null,
        Func<ToolInvocation, Task>? toolCallback = null,
        CancellationToken ct = default)
    {
        var persisted = await agentService.GetChatByAgent(agentId);
        var chat = CloneTransient(persisted);
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

        await chatRepository.AddChat(chat);
        try
        {
            var resultChat = await agentService.Process(chat, agentId, knowledge: null, translatePrompt: false,
                callbackToken: tokenCallback, callbackTool: toolCallback);

            var reply = resultChat.Messages.LastOrDefault();
            return new AgentRunResult { ModelId = resultChat.ModelId, Content = reply?.Content ?? string.Empty };
        }
        finally
        {
            await chatRepository.DeleteChat(chat.Id);
        }
    }

    private static Chat CloneTransient(Chat source) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = source.Name,
        ModelId = source.ModelId,
        Messages = source.Messages.Select(CloneMessage).ToList(),
        Type = source.Type,
        ImageGen = source.ImageGen,
        BackendParams = source.BackendParams,
        MemoryParams = source.MemoryParams,
        ToolsConfiguration = source.ToolsConfiguration,
        ProviderSkillReferences = [.. source.ProviderSkillReferences],
        TextToSpeechParams = source.TextToSpeechParams,
        Properties = new Dictionary<string, string>(source.Properties),
        Interactive = source.Interactive,
        Translate = source.Translate,
    };

    private static Message CloneMessage(Message source) => new()
    {
        Role = source.Role,
        Content = source.Content,
        Type = source.Type,
        Tokens = [.. source.Tokens],
        Tool = source.Tool,
        Time = source.Time,
        Images = source.Images,
        Speech = source.Speech,
        Files = source.Files,
        Properties = new Dictionary<string, string>(source.Properties),
    };
}
