using MaIN.InferPage.Endpoints;

namespace MaIN.InferPage.UnitTests;

public class AgentModelRefTests
{
    [Theory]
    [InlineData("agent:abc-123", true, "abc-123")]
    [InlineData("agent:My Agent", true, "My Agent")]
    [InlineData("AGENT:abc", true, "abc")]
    [InlineData("gpt-4o", false, "")]
    [InlineData("agent:", false, "")]
    [InlineData(null, false, "")]
    [InlineData("  ", false, "")]
    public void TryParse_detects_agent_references(string? model, bool expected, string expectedId)
    {
        var result = AgentModelRef.TryParse(model, out var agentId);
        Assert.Equal(expected, result);
        Assert.Equal(expectedId, agentId);
    }

    [Fact]
    public void Format_prefixes_an_agent_id() => Assert.Equal("agent:abc-123", AgentModelRef.Format("abc-123"));
}
