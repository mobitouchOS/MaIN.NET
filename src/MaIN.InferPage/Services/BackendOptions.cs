using MaIN.Domain.Configuration;

namespace MaIN.InferPage.Services;

public sealed record BackendOption(int Id, string DisplayName, BackendType BackendType, bool RequiresApiKey);

public static class BackendOptions
{
    public static List<BackendOption> All => new List<BackendOption>
    {
        new(1, "OpenAI", BackendType.OpenAi, true),
        new(2, "Gemini", BackendType.Gemini, true),
        new(3, "DeepSeek", BackendType.DeepSeek, true),
        new(4, "GroqCloud", BackendType.GroqCloud, true),
        new(5, "Anthropic", BackendType.Anthropic, true),
        new(6, "xAI", BackendType.Xai, true),
        new(7, "Ollama (Local)", BackendType.Ollama, false),
        new(8, "Ollama (Cloud)", BackendType.Ollama, true),
        new(9, "Vertex AI", BackendType.Vertex, false)
    }.OrderBy(x => x.DisplayName)
     .Prepend(new BackendOption(0, "Local", BackendType.Self, false))
     .ToList();
}
