namespace MaIN.Domain.Models.Abstract;

/// <summary>
/// Interface for models that support vision/image input capabilities.
/// </summary>
public interface IVisionModel
{
    /// <summary>
    /// Name of the multimodal projector file. (must be in the same location as the model)
    /// Null for cloud models (handled by provider API).
    /// </summary>
    string? MMProjectName { get; }
}

/// <summary>
/// Interface for models that support reasoning/thinking capabilities.
/// </summary>
public interface IReasoningModel
{
    /// <summary>
    /// Function to process reasoning tokens.
    /// Null for cloud models (reasoning handled by provider API).
    /// </summary>
    Func<string, ThinkingState, LLMTokenValue>? ReasonFunction { get; }

    /// <summary>
    /// Additional prompt added to enable reasoning mode.
    /// </summary>
    string? AdditionalPrompt { get; }
}

// TODO: use it with existing embedding model
/// <summary>
/// Interface for models that support embeddings generation.
/// </summary>
public interface IEmbeddingModel
{
    /// <summary>
    /// Dimension of the embedding vector.
    /// </summary>
    int EmbeddingDimension { get; }
}

/// <summary>
/// Interface for models that support text-to-speech.
/// </summary>
public interface ITTSModel;

/// <summary>
/// Interface for models that generate images from text prompts.
/// </summary>
public interface IImageGenerationModel;

/// <summary>
/// Diffusion model architecture, used to pick sensible generation defaults and required text-encoder/VAE assets.
/// </summary>
public enum DiffusionArchitecture
{
    SD1,
    SDXL,
    SD3,
    Flux,
    QwenImage
}

/// <summary>
/// Interface for local GGUF diffusion models that generate images in-process via stable-diffusion.cpp.
/// </summary>
public interface ILocalDiffusionModel : IImageGenerationModel
{
    /// <summary> Diffusion model architecture (affects defaults and required encoder/VAE assets). </summary>
    DiffusionArchitecture Architecture { get; }

    /// <summary> Default output image width in pixels. </summary>
    int Width { get; }

    /// <summary> Default output image height in pixels. </summary>
    int Height { get; }

    /// <summary> Default number of sampling steps. </summary>
    int Steps { get; }

    /// <summary> Default classifier-free guidance scale. </summary>
    float CfgScale { get; }

    /// <summary> Optional separate VAE file. Null if the VAE is embedded in the main model file. </summary>
    ModelAsset? Vae { get; }

    /// <summary> Optional CLIP-L text encoder file. </summary>
    ModelAsset? ClipL { get; }

    /// <summary> Optional CLIP-G text encoder file (SDXL/SD3). </summary>
    ModelAsset? ClipG { get; }

    /// <summary> Optional T5-XXL text encoder file (SD3/FLUX). </summary>
    ModelAsset? T5Xxl { get; }

    /// <summary> Optional Qwen2.5-VL text encoder file (Qwen-Image). </summary>
    ModelAsset? Qwen2VL { get; }
}
