using MaIN.Core.Hub;
using MaIN.Core.Hub.Contexts.Interfaces.AgentContext;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Agents;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions.Agents;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Repositories;
using MaIN.Services.Constants;
using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.InferPage.Services;

/// <summary>
/// SECURITY: Command is launched verbatim as an OS process by the MCP step
/// handler, unsandboxed, with the host's privileges. Only ever populated when the host operator
/// opted in via MaIN:AllowMcpConfiguration.
/// </summary>
public sealed record McpServerRequest(
    string Name,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentVariables);

public sealed record CreateAgentRequest(
    string Name,
    string ModelId,
    string SystemPrompt,
    IReadOnlyList<string> ToolNames,
    McpServerRequest? Mcp = null);

public sealed class AgentDefinitionService(
    IAgentRepository agentRepository,
    IChatRepository chatRepository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private string? SearxngBaseUrl => configuration["MaIN:SearxngBaseUrl"];

    // Invalidated on every Create/Update/Delete below -- avoids re-reading every agent's JSON file
    // from disk on each call (e.g. /v1/models hits this on every request).
    private volatile List<Agent>? _cachedAgents;

    public async Task<List<Agent>> GetAllAsync()
    {
        var cached = _cachedAgents;
        if (cached is not null)
        {
            return cached;
        }

        var agents = await AIHub.Agent().GetAllAgents();
        _cachedAgents = agents;
        return agents;
    }

    public Task<Agent?> GetByIdAsync(string id) => AIHub.Agent().GetAgentById(id);

    public async Task<Agent> CreateAsync(CreateAgentRequest request)
    {
        var builder = AIHub.Agent()
            .WithModel(request.ModelId)
            .WithName(request.Name)
            .WithInitialPrompt(request.SystemPrompt);

        var toolsConfig = BuildToolsConfiguration(request.ToolNames);
        if (toolsConfig is not null)
        {
            builder = builder.WithTools(toolsConfig);
        }

        builder = ApplyMcpConfig(builder, request);

        var executor = await builder.CreateAsync();
        _cachedAgents = null;
        return executor.GetAgent();
    }

    public async Task<Agent> UpdateAsync(string id, CreateAgentRequest request)
    {
        if (!ModelRegistry.Exists(request.ModelId))
        {
            throw new AgentModelNotAvailableException(id, request.ModelId);
        }

        var agent = await agentRepository.GetAgentById(id) ?? throw new AgentNotFoundException(id);

        var toolsConfig = BuildToolsConfiguration(request.ToolNames);
        ApplyAgentUpdate(agent, request, toolsConfig);
        await agentRepository.UpdateAgent(id, agent);

        // Update the existing chat's model/tools/system message in place -- renaming or reconfiguring
        // an agent must not touch its conversation history.
        var chat = await chatRepository.GetChatById(agent.ChatId);
        if (chat is not null)
        {
            chat.ModelId = agent.Model;
            chat.ToolsConfiguration = toolsConfig;
            chat.ImageGen = ModelRegistry.TryGetById(agent.Model, out var agentModel) && agentModel!.HasImageGeneration;

            var backend = ModelRegistry.TryGetById(agent.Model, out var model) ? model!.Backend : BackendType.Self;
            var systemMessageType = backend != BackendType.Self ? MessageType.CloudLLM : MessageType.LocalLLM;
            var systemMessage = chat.Messages.FirstOrDefault(m =>
                m.Role.Equals(ServiceConstants.Roles.System, StringComparison.OrdinalIgnoreCase));
            if (systemMessage is not null)
            {
                systemMessage.Content = request.SystemPrompt;
                systemMessage.Type = systemMessageType;
            }
            else
            {
                chat.Messages.Insert(0, new Message
                {
                    Role = "System",
                    Content = request.SystemPrompt,
                    Type = systemMessageType
                });
            }

            await chatRepository.UpdateChat(chat.Id, chat);
        }

        _cachedAgents = null;
        return agent;
    }

    // Mirrors ApplyMcpConfig's create-time behavior, but update must also handle turning MCP *off*:
    // an agent previously configured with Steps=["MCP"] has to fall back to ["ANSWER"] or it's left
    // calling the MCP step handler with no McpConfig.
    private static void ApplyAgentUpdate(Agent agent, CreateAgentRequest request, ToolsConfiguration? toolsConfig)
    {
        agent.Name = request.Name;
        agent.Model = request.ModelId;
        agent.Config.Instruction = request.SystemPrompt;
        agent.ToolsConfiguration = toolsConfig;

        if (request.Mcp is null)
        {
            agent.Config.McpConfig = null;
            agent.Config.Steps = StepBuilder.Instance.Answer().Build();
            return;
        }

        agent.Config.McpConfig = new Mcp
        {
            Name = request.Mcp.Name,
            Command = request.Mcp.Command,
            Arguments = request.Mcp.Arguments.ToList(),
            EnvironmentVariables = request.Mcp.EnvironmentVariables.ToDictionary(kv => kv.Key, kv => kv.Value),
            Model = request.ModelId,
            // Mirrors AgentContext.WithMcpConfig, which stamps this from the agent's model backend.
            Backend = ModelRegistry.GetById(request.ModelId).Backend
        };
        agent.Config.Steps = StepBuilder.Instance.Mcp().Build();
    }

    public async Task DeleteAsync(string id)
    {
        var ctx = await AIHub.Agent().FromExisting(id);
        await ctx.Delete();
        _cachedAgents = null;
    }

    // No MCP config => leave the agent's default Steps (["ANSWER"]) alone. Setting McpConfig without
    // an explicit "MCP" step is a no-op, so the step list is only rewritten when MCP is actually used.
    private static IAgentConfigurationBuilder ApplyMcpConfig(IAgentConfigurationBuilder builder, CreateAgentRequest request)
    {
        if (request.Mcp is null)
        {
            return builder;
        }

        return builder
            .WithMcpConfig(new Mcp
            {
                Name = request.Mcp.Name,
                Command = request.Mcp.Command,
                Arguments = request.Mcp.Arguments.ToList(),
                EnvironmentVariables = request.Mcp.EnvironmentVariables.ToDictionary(kv => kv.Key, kv => kv.Value),
                Model = request.ModelId // McpService sends this as the OpenAI "model" field — empty means a 400.
            })
            // MCP already calls the model, runs tools, and synthesizes the final reply. A trailing "ANSWER"
            // step re-runs a tool-less chat completion afterward, and StepHandlerExtensions.EnsureUserMessageReadiness
            // reorders the user's message to the end of history — after the MCP reply — which confuses that
            // second call into ignoring the tool result. Library's own MCP example never chains ANSWER after MCP.
            .WithSteps(StepBuilder.Instance.Mcp().Build());
    }

    private ToolsConfiguration? BuildToolsConfiguration(IReadOnlyList<string> toolNames)
    {
        if (toolNames.Count == 0)
        {
            return null;
        }

        var tools = new List<ToolDefinition>();
        foreach (var name in toolNames)
        {
            if (BuiltInToolCatalog.IsKnown(name)
                && HostedToolsResolver.TryResolveBuiltInTool(name, httpClientFactory, out var def, SearxngBaseUrl))
            {
                tools.Add(def);
            }
        }

        return tools.Count == 0 ? null : new ToolsConfiguration { Tools = tools };
    }
}
