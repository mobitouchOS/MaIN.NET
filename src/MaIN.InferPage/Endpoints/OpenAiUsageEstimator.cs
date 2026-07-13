namespace MaIN.InferPage.Endpoints;

public static class OpenAiUsageEstimator
{
    public static ChatCompletionUsage Estimate(string promptText, string completionText)
    {
        var promptTokens = EstimateTokenCount(promptText);
        var completionTokens = EstimateTokenCount(completionText);

        return new ChatCompletionUsage
        {
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens
        };
    }

    // MaIN has no native token counter -- ~4 characters per token is the commonly used
    // rough approximation for English text across tokenizers.
    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }
}
