using MaIN.InferPage.Endpoints;

namespace MaIN.InferPage.UnitTests;

public class OpenAiUsageEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsPositiveTokenCounts_ForNonEmptyText()
    {
        var usage = OpenAiUsageEstimator.Estimate("Hello, how are you?", "I'm doing well, thanks!");

        Assert.True(usage.PromptTokens > 0);
        Assert.True(usage.CompletionTokens > 0);
        Assert.Equal(usage.PromptTokens + usage.CompletionTokens, usage.TotalTokens);
    }

    [Fact]
    public void Estimate_ReturnsZero_ForEmptyText()
    {
        var usage = OpenAiUsageEstimator.Estimate(string.Empty, string.Empty);

        Assert.Equal(0, usage.PromptTokens);
        Assert.Equal(0, usage.CompletionTokens);
        Assert.Equal(0, usage.TotalTokens);
    }

    [Fact]
    public void Estimate_ScalesRoughlyWithTextLength()
    {
        var shortUsage = OpenAiUsageEstimator.Estimate("hi", string.Empty);
        var longUsage = OpenAiUsageEstimator.Estimate(new string('a', 400), string.Empty);

        Assert.True(longUsage.PromptTokens > shortUsage.PromptTokens);
    }
}
