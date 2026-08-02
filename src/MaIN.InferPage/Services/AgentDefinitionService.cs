using MaIN.Core.Hub;
using MaIN.Core.Hub.Contexts.Interfaces.AgentContext;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Agents;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions.Agents;
using MaIN.Domain.Models.Abstract;
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

public sealed class AgentDefinitionService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private string? SearxngBaseUrl => configuration["MaIN:SearxngBaseUrl"];

    public Task<List<Agent>> GetAllAsync() => AIHub.Agent().GetAllAgents();

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
        return executor.GetAgent();
    }

    public async Task<Agent> UpdateAsync(string id, CreateAgentRequest request)
    {
        // Validate the model before deleting so a bad model id can't lose the agent. Note: delete+recreate is
        // not atomic — a transient failure during the recreate below would lose the agent (acceptable for this admin panel).
        if (!ModelRegistry.Exists(request.ModelId))
        {
            throw new AgentModelNotAvailableException(id, request.ModelId);
        }

        await DeleteAsync(id);

        // Recreate with the same id (keeps agent:<id> refs + active-agent selection valid),
        // rebuilding the agent's chat with the new prompt/tools.
        var builder = AIHub.Agent()
            .WithModel(request.ModelId)
            .WithId(id)
            .WithName(request.Name)
            .WithInitialPrompt(request.SystemPrompt);

        var toolsConfig = BuildToolsConfiguration(request.ToolNames);
        if (toolsConfig is not null)
        {
            builder = builder.WithTools(toolsConfig);
        }

        builder = ApplyMcpConfig(builder, request);

        var executor = await builder.CreateAsync();
        return executor.GetAgent();
    }

    public async Task DeleteAsync(string id)
    {
        var ctx = await AIHub.Agent().FromExisting(id);
        await ctx.Delete();
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
