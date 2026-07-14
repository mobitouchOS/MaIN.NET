using System.Diagnostics.CodeAnalysis;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class HostedToolsResolver
{
    public static bool TryResolveBuiltInTool(
        string typeOrName,
        IHttpClientFactory? httpClientFactory,
        [NotNullWhen(true)] out ToolDefinition? tool)
    {
        if (string.IsNullOrWhiteSpace(typeOrName))
        {
            tool = null;
            return false;
        }

        var normalized = typeOrName.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "web_search":
            case "web_search_preview":
            case "search_web":
                tool = WebSearchTool.Create(httpClientFactory, toolName: normalized);
                return true;

            case "fetch_web_page":
            case "read_url":
                tool = WebPageFetchTool.Create(httpClientFactory, toolName: normalized);
                return true;

            case "get_current_datetime":
            case "current_time":
            case "datetime":
                tool = DateTimeTool.Create(toolName: normalized);
                return true;

            case "calculator":
            case "math_eval":
                tool = CalculatorTool.Create(toolName: normalized);
                return true;

            default:
                tool = null;
                return false;
        }
    }

    public static List<ToolDefinition> GetAllBuiltInTools(IHttpClientFactory? httpClientFactory = null)
    {
        return
        [
            WebSearchTool.Create(httpClientFactory),
            WebPageFetchTool.Create(httpClientFactory),
            DateTimeTool.Create(),
            CalculatorTool.Create()
        ];
    }
}
