using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.Vertex;
using MaIN.Domain.Entities;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;

namespace MaIN.InferPage;

public static class Utils
{
    public static BackendType BackendType { get; set; } = BackendType.Self;
    public static bool HasApiKey { get; set; }
    public static string? Path { get; set; }
    public static bool IsLocal => BackendType == BackendType.Self || (BackendType == BackendType.Ollama && !HasApiKey);
    public static string? Model;

    public static bool NeedsConfiguration { get; set; }

    public static string DefaultModelsPath => System.IO.Path.Combine(Directory.GetCurrentDirectory(), "models");

    /// <summary>
    /// Turns an arbitrary model name (used as a registry id when paired with modelUrl)
    /// into a safe .gguf filename for the models directory.
    /// </summary>
    public static string SanitizeModelFileName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "custom-model.gguf";
        }

        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(model.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "custom-model";
        }

        return sanitized.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
            ? sanitized
            : sanitized + ".gguf";
    }

    // Manual capability overrides for unregistered models (set from Settings UI)
    public static bool? ManualVision { get; set; }
    public static bool? ManualReasoning { get; set; }
    public static bool? ManualImageGen { get; set; }
    public static string? MmProjName { get; set; }

    // registry → manual override → fallback set (null = no fallback)
    private static bool GetCapability<T>(bool? manual, HashSet<string>? fallback = null)
        where T : class =>
        !string.IsNullOrEmpty(Model) && (
            ModelRegistry.TryGetById(Model, out var m) ? m is T :
            manual.HasValue ? manual.Value :
            fallback?.Contains(Model) ?? false);

    public static bool ImageGen => GetCapability<IImageGenerationModel>(ManualImageGen, ImageGenerationModels);
    public static bool Vision   => GetCapability<IVisionModel>(ManualVision, VisionModels);
    public static bool Reason   => GetCapability<IReasoningModel>(ManualReasoning);

    public static bool IsKnownVisionModel(string model) => VisionModels.Contains(model);
    public static bool IsKnownImageGenModel(string model) => ImageGenerationModels.Contains(model);

    public static void ApplySettings(
        BackendType backendType,
        string model,
        string? modelPath,
        bool hasVision,
        bool hasReasoning,
        bool hasImageGen,
        string? mmProjName,
        MaINSettings mainSettings,
        string? apiKey,
        GoogleServiceAccountConfig? vertexAuth = null)
    {
        BackendType = backendType;
        Model = model;
        // Strip leading/trailing whitespace and invisible Unicode control/format characters
        // (e.g. U+202A LEFT-TO-RIGHT EMBEDDING, U+FEFF BOM — added by Windows Explorer on copy-paste)
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var cleaned = modelPath.Trim();
            int i = 0;
            while (i < cleaned.Length &&
                   (char.IsControl(cleaned[i]) ||
                    char.GetUnicodeCategory(cleaned[i]) == System.Globalization.UnicodeCategory.Format))
                i++;
            Path = i < cleaned.Length ? cleaned.Substring(i) : null;
        }
        else
        {
            Path = null;
        }

        if (backendType == BackendType.Self && Path == null)
        {
            // Prefer an already-configured path (e.g. MaIN_ModelsPath set by Docker ENV) so that
            // volume mounts are respected without the user having to re-enter the path.
            var defaultPath = Environment.GetEnvironmentVariable("MaIN_ModelsPath")
                ?? (!string.IsNullOrEmpty(mainSettings.ModelsPath) ? mainSettings.ModelsPath : null)
                ?? DefaultModelsPath;
            Directory.CreateDirectory(defaultPath);
            Path = defaultPath;
            mainSettings.ModelsPath = defaultPath;
            Environment.SetEnvironmentVariable("MaIN_ModelsPath", defaultPath);
        }

        HasApiKey = !string.IsNullOrEmpty(apiKey);
        NeedsConfiguration = false;

        ManualVision = hasVision;
        ManualReasoning = hasReasoning;
        ManualImageGen = hasImageGen;
        MmProjName = string.IsNullOrWhiteSpace(mmProjName) ? null : mmProjName.Trim();

        mainSettings.BackendType = backendType;

        // null clears env var and key; handles both "set new key" and "clear stale key" cases
        var entry = LLMApiRegistry.GetEntry(backendType);
        if (entry != null)
            Environment.SetEnvironmentVariable(entry.ApiKeyEnvName, apiKey);

        switch (backendType)
        {
            case BackendType.OpenAi: mainSettings.OpenAiKey = apiKey; break;
            case BackendType.Gemini: mainSettings.GeminiKey = apiKey; break;
            case BackendType.DeepSeek: mainSettings.DeepSeekKey = apiKey; break;
            case BackendType.Anthropic: mainSettings.AnthropicKey = apiKey; break;
            case BackendType.GroqCloud: mainSettings.GroqCloudKey = apiKey; break;
            case BackendType.Ollama: mainSettings.OllamaKey = apiKey; break;
            case BackendType.Xai: mainSettings.XaiKey = apiKey; break;
            case BackendType.Vertex:
                if (vertexAuth != null)
                    mainSettings.GoogleServiceAccountAuth = vertexAuth;
                break;
        }
    }

    private static readonly HashSet<string> ImageGenerationModels =
    [
        "FLUX.1_Shnell", "FLUX.1",
        "dall-e-3", "dall-e",
        "gpt-image-1",
        "imagen", "imagen-3", "imagen-4", "imagen-4-fast",
        "grok-2-image"
    ];

    private static readonly HashSet<string> VisionModels =
    [
        "gpt-4o", "gpt-4o-mini",
        "claude-3-opus", "claude-3-sonnet", "claude-3-haiku",
        "claude-3-5-sonnet", "claude-3-5-haiku", "claude-3-7-sonnet",
        "gemini-1.5-pro", "gemini-1.5-flash", "gemini-2.0-flash", "gemini-2.0-flash-lite",
        "llava", "llava-1.6", "llava-phi3",
        "gemma3", "gemma3-4b", "gemma3-12b", "gemma3-27b"
    ];
}

public class MessageExt
{
    public required Message Message { get; set; }
    public bool ShowReason { get; set; }
    public bool HasReasoning { get; set; }
    public string ComputedContent { get; set; } = string.Empty;
    public string ComputedReasoning { get; set; } = string.Empty;
    public List<string> AttachedFiles { get; set; } = new();
    public List<(string Name, string Base64)> AttachedImages { get; set; } = new();
}