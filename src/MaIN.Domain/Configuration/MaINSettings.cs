using MaIN.Domain.Configuration.Vertex;

namespace MaIN.Domain.Configuration;

public class MaINSettings
{
    public BackendType BackendType { get; set; } = BackendType.Self;
    public string? ModelsPath { get; set; }
    public string? OpenAiKey { get; set; }
    public string? GeminiKey { get; set; }
    public string? DeepSeekKey { get; set; }
    public string? AnthropicKey { get; set; }
    public string? GroqCloudKey { get; set; }
    public string? OllamaKey { get; set; }
    public string? OllamaBaseUrl { get; set; }
    public string? XaiKey { get; set; }
    public MongoDbSettings? MongoDbSettings { get; set; }
    public FileSystemSettings? FileSystemSettings { get; set; }
    public SqliteSettings? SqliteSettings { get; set; }
    public SqlSettings? SqlSettings { get; set; }
    public string? VoicesPath { get; set; }
    public GoogleServiceAccountConfig? GoogleServiceAccountAuth { get; set; }
    public string? SkillsDirectory { get; set; }
    public SkillUploadSettings SkillUpload { get; set; } = new();
}

public class SkillUploadSettings
{
    /// <summary>Path to the on-disk cache of provider skill ids. Avoids re-uploading bundles on each run.</summary>
    public string CacheFilePath { get; set; } = Path.Combine(".main", "skills-cache.json");

    /// <summary>
    /// When true, the coordinator probes the provider's existing skill list (GET /v1/skills) on first
    /// use per backend to recover cached skill_ids after a cache wipe; degrades gracefully if the endpoint 404s.
    /// </summary>
    public bool ReconcileWithProvider { get; set; } = true;

    /// <summary>
    /// When true, SkillComposer throws <see cref="MaIN.Domain.Exceptions.Skills.SkillNotSupportedException"/>
    /// for any uploadable skill that can't be routed through a provider's native Skills API; default false silently
    /// falls back to inline composition. Code-defined skills (Execute delegate) are exempt and always compose locally.
    /// </summary>
    public bool RequireNativeSkillsApi { get; set; } = false;
}

public enum BackendType
{
    Self = 0,
    OpenAi = 1,
    Gemini = 2,
    DeepSeek = 3,
    GroqCloud = 4,
    Anthropic = 5,
    Xai = 6,
    Ollama = 7,
    Vertex = 8,
}