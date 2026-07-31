using MaIN.InferPage.Services;
using MaIN.Services.Services.LLMService.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace MaIN.InferPage.UnitTests;

public class BuiltInToolCatalogTests
{
    [Fact]
    public void All_returns_the_six_resolvable_built_in_tools()
    {
        var names = BuiltInToolCatalog.All.Select(t => t.Name).ToList();

        Assert.Equal(6, names.Count);
        Assert.Contains("web_search", names);
        Assert.Contains("fetch_web_page", names);
        Assert.Contains("get_current_datetime", names);
        Assert.Contains("http_request", names);
        Assert.Contains("rss_feed_reader", names);
        Assert.Contains("extract_url_metadata", names);
        Assert.DoesNotContain("search_documents", names);
    }

    [Fact]
    public void Every_entry_has_a_display_name_and_description()
    {
        Assert.All(BuiltInToolCatalog.All, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
        });
    }

    [Fact]
    public void Every_catalog_tool_is_resolvable_by_the_hosted_tools_resolver()
    {
        var factory = new ServiceCollection().AddHttpClient()
            .BuildServiceProvider().GetRequiredService<IHttpClientFactory>();

        foreach (var tool in BuiltInToolCatalog.All)
        {
            Assert.True(
                HostedToolsResolver.TryResolveBuiltInTool(tool.Name, factory, out _),
                $"Catalog tool '{tool.Name}' is not resolvable by HostedToolsResolver.");
        }
    }

    [Fact]
    public void Display_names_use_overrides_then_prettified_fallback()
    {
        string Display(string name) => BuiltInToolCatalog.All.Single(t => t.Name == name).DisplayName;

        Assert.Equal("HTTP Request", Display("http_request"));         // curated override
        Assert.Equal("URL Metadata", Display("extract_url_metadata")); // curated override
        Assert.Equal("Web Search", Display("web_search"));             // prettified from snake_case, no override
    }
}
