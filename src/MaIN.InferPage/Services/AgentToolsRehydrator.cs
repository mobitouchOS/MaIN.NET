using MaIN.Domain.Entities;
using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.InferPage.Services;

/// <summary>Re-attaches built-in tool executors (ToolDefinition.Execute is [JsonIgnore], lost on load) by resolving each tool name.</summary>
public static class AgentToolsRehydrator
{
    public static void Rehydrate(Chat chat, IHttpClientFactory? httpClientFactory, string? searxngBaseUrl)
    {
        var tools = chat.ToolsConfiguration?.Tools;
        if (tools is null || tools.Count == 0)
        {
            return;
        }

        foreach (var tool in tools)
        {
            // IsClientSide is also [JsonIgnore] (lost on load), so matching is by name only — bounded edge case.
            if (tool.Execute is not null)
            {
                continue;
            }

            var name = tool.Function?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (HostedToolsResolver.TryResolveBuiltInTool(name, httpClientFactory, out var live, searxngBaseUrl)
                && live is not null)
            {
                tool.Execute = live.Execute;
            }
        }
    }
}
