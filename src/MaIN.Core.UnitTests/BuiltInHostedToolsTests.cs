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
    [InlineData("calculator", "calculator")]
    [InlineData("math_eval", "math_eval")]
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
    public async Task CalculatorTool_EvaluatesMathExpressions()
    {
        var tool = CalculatorTool.Create();
        var result = await tool.Execute!.Invoke("""{"expression":"15 * 4 + 10"}""");

        Assert.Contains("70", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetAllBuiltInTools_ReturnsAllFourBuiltInTools()
    {
        var tools = HostedToolsResolver.GetAllBuiltInTools();

        Assert.Equal(4, tools.Count);
        Assert.All(tools, t => Assert.False(t.IsClientSide));
        Assert.All(tools, t => Assert.NotNull(t.Execute));
    }
}
