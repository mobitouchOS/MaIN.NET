using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.Core.UnitTests;

public class BuiltInHostedToolsTests
{
    [Theory]
    [InlineData("web_search", "web_search")]
    [InlineData("web_search_preview", "web_search_preview")]
    [InlineData("search_web", "search_web")]
    [InlineData("fetch_web_page", "fetch_web_page")]
    [InlineData("read_url", "read_url")]
    [InlineData("get_current_datetime", "get_current_datetime")]
    [InlineData("datetime", "datetime")]
    [InlineData("http_request", "http_request")]
    [InlineData("fetch_api", "fetch_api")]
    [InlineData("rss_feed_reader", "rss_feed_reader")]
    [InlineData("read_rss", "read_rss")]
    [InlineData("extract_url_metadata", "extract_url_metadata")]
    [InlineData("link_preview", "link_preview")]
    public void TryResolveBuiltInTool_ResolvesKnownNamesAndTypes(string inputName, string expectedName)
    {
        var resolved = HostedToolsResolver.TryResolveBuiltInTool(inputName, null, out var tool);

        Assert.True(resolved);
        Assert.NotNull(tool);
        Assert.False(tool.IsClientSide);
        Assert.Equal(expectedName, tool.Function!.Name);
        Assert.NotNull(tool.Execute);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryResolveBuiltInTool_ReturnsFalseForUnknownTool()
    {
        var resolved = HostedToolsResolver.TryResolveBuiltInTool("unknown_custom_tool", null, out var tool);

        Assert.False(resolved);
        Assert.Null(tool);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DateTimeTool_ReturnsCurrentUtcTime()
    {
        var tool = DateTimeTool.Create();
        var result = await tool.Execute!.Invoke("{}");

        Assert.Contains("UTC", result);
        Assert.Contains(DateTimeOffset.UtcNow.Year.ToString(), result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HttpRequestTool_ReturnsErrorForMissingUrl()
    {
        var tool = HttpRequestTool.Create();
        var result = await tool.Execute!.Invoke("{}");

        Assert.Contains("Missing required parameter 'url'", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RssFeedReaderTool_ReturnsErrorForInvalidUrl()
    {
        var tool = RssFeedReaderTool.Create();
        var result = await tool.Execute!.Invoke("""{"feed_url":"not_a_valid_url"}""");

        Assert.Contains("Invalid feed URL", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UrlMetadataExtractorTool_ReturnsErrorForInvalidUrl()
    {
        var tool = UrlMetadataExtractorTool.Create();
        var result = await tool.Execute!.Invoke("""{"url":"ftp://invalid.schema"}""");

        Assert.Contains("Invalid URL schema", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetAllBuiltInTools_ReturnsAllSixBuiltInTools()
    {
        var tools = HostedToolsResolver.GetAllBuiltInTools();

        Assert.Equal(6, tools.Count);
        Assert.All(tools, t => Assert.False(t.IsClientSide));
        Assert.All(tools, t => Assert.NotNull(t.Execute));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToolsConfiguration_CrossMatchesWebSearchAndPreview()
    {
        HostedToolsResolver.TryResolveBuiltInTool("web_search", null, out var newKindTool);
        var configWithNewKind = new MaIN.Domain.Entities.Tools.ToolsConfiguration { Tools = [newKindTool!] };

        Assert.NotNull(configWithNewKind.GetDefinition("web_search"));
        Assert.NotNull(configWithNewKind.GetDefinition("web_search_preview"));
        Assert.NotNull(configWithNewKind.GetExecutor("web_search_preview"));

        HostedToolsResolver.TryResolveBuiltInTool("web_search_preview", null, out var previewTool);
        var configWithPreview = new MaIN.Domain.Entities.Tools.ToolsConfiguration { Tools = [previewTool!] };

        Assert.NotNull(configWithPreview.GetDefinition("web_search"));
        Assert.NotNull(configWithPreview.GetDefinition("web_search_preview"));
        Assert.NotNull(configWithPreview.GetExecutor("web_search"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToolConfigurationBuilder_AddDefaultTool_ResolvesWebSearchAndWebSearchPreview()
    {
        var builder = new MaIN.Core.Hub.Utils.ToolsConfigurationBuilder()
            .AddDefaultTool("web_search")
            .AddDefaultTool("web_search_preview");

        var config = builder.Build();
        Assert.Equal(2, config.Tools.Count);
        Assert.All(config.Tools, t => Assert.NotNull(t.Execute));
        Assert.Equal("web_search", config.Tools[0].Type);
        Assert.Equal("web_search_preview", config.Tools[1].Type);
    }
}
