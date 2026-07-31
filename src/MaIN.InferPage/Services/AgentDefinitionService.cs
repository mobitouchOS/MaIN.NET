using MaIN.Core.Hub;
using MaIN.Domain.Entities.Agents;
using MaIN.Domain.Entities.Tools;
using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.InferPage.Services;

public sealed record CreateAgentRequest(
    string Name,
    string ModelId,
    string SystemPrompt,
    IReadOnlyList<string> ToolNames);

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

        var executor = await builder.CreateAsync();
        return executor.GetAgent();
    }

    public async Task DeleteAsync(string id)
    {
        var ctx = await AIHub.Agent().FromExisting(id);
        await ctx.Delete();
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
