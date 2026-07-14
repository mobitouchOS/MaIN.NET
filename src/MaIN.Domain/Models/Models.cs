namespace MaIN.Domain.Models;

/// <summary>
/// Compile-time constants for all pre-built model IDs.
/// Use these with <c>WithModel(string modelId)</c> to avoid magic strings.
/// </summary>
public static class Models
{
    public static class OpenAi
    {
        public const string Gpt4o = "gpt-4o";
        public const string Gpt4oMini = "gpt-4o-mini";
        public const string Gpt4_1 = "gpt-4.1";
        public const string Gpt4_1Mini = "gpt-4.1-mini";
        public const string Gpt5 = "gpt-5";
        public const string Gpt5Mini = "gpt-5-mini";
        public const string Gpt5Nano = "gpt-5-nano";
        public const string Gpt5_1 = "gpt-5.1";
        public const string Gpt5_2 = "gpt-5.2";
        public const string Gpt5_4 = "gpt-5.4";
        public const string Gpt5_4Mini = "gpt-5.4-mini";
        public const string Gpt5_4Nano = "gpt-5.4-nano";
        public const string Gpt5_5 = "gpt-5.5";
        public const string Gpt5_5Pro = "gpt-5.5-pro";
        public const string CodexMini = "codex-mini-latest";
        public const string O3 = "o3";
        public const string O3Mini = "o3-mini";
        public const string O4Mini = "o4-mini";
        public const string DallE3 = "dall-e-3";
        public const string GptImage1 = "gpt-image-1";
    }

    public static class Anthropic
    {
        public const string ClaudeOpus4_7 = "claude-opus-4-7";
        public const string ClaudeSonnet4_6 = "claude-sonnet-4-6";
        public const string ClaudeHaiku4_5 = "claude-haiku-4-5-20251001";
        public const string ClaudeSonnet4 = "claude-sonnet-4-20250514";
        public const string ClaudeSonnet4_5 = "claude-sonnet-4-5-20250929";
        public const string ClaudeSonnet5 = "claude-sonnet-5";
    }

    public static class Gemini
    {
        public const string Gemini3_5Flash = "gemini-3.5-flash";
        public const string Gemini3_1FlashLite = "gemini-3.1-flash-lite";
        public const string Gemini3_1ProPreview = "gemini-3.1-pro-preview";
        public const string Gemini3_1FlashImagePreview = "gemini-3.1-flash-image-preview";
        public const string Gemini2_5Pro = "gemini-2.5-pro";
        public const string Gemini2_5Flash = "gemini-2.5-flash";
        public const string Gemini2_0Flash = "gemini-2.0-flash";
        public const string Imagen4_0_FastGenerate = "imagen-4.0-fast-generate-001";
        public const string NanoBanana = "gemini-2.5-flash-image";
    }

    public static class Xai
    {
        public const string Grok4_20Reasoning = "grok-4.20-reasoning";
        public const string Grok4_20NonReasoning = "grok-4.20-non-reasoning";
        public const string Grok4_1FastReasoning = "grok-4-1-fast-reasoning";
        public const string Grok4_1FastNonReasoning = "grok-4-1-fast-non-reasoning";
        public const string GrokImagineImage = "grok-imagine-image";
        public const string GrokImagineImagePro = "grok-imagine-image-pro";
        public const string Grok3Beta = "grok-3-beta";
        public const string GrokImage = "grok-2-image";
    }

    public static class Groq
    {
        public const string Llama4Scout17b = "meta-llama/llama-4-scout-17b-16e-instruct";
        public const string Llama3_3_70b = "llama-3.3-70b-versatile";
        public const string Llama3_1_8b = "llama-3.1-8b-instant";
        public const string Qwen3_32b = "qwen/qwen3-32b";
        public const string Compound = "groq/compound";
        public const string CompoundMini = "groq/compound-mini";
        public const string GptOss120b = "openai/gpt-oss-120b";
        public const string GptOss20b = "openai/gpt-oss-20b";
    }

    public static class DeepSeek
    {
        public const string Chat = "deepseek-chat";
        public const string Reasoner = "deepseek-reasoner";
    }

    public static class Ollama
    {
        // Llama 4
        public const string Llama4Scout = "llama4:scout";
        public const string Llama4Maverick = "llama4:maverick";

        // Gemma 3
        public const string Gemma3_4b = "gemma3:4b";
        public const string Gemma3_12b = "gemma3:12b";
        public const string Gemma3_27b = "gemma3:27b";

        // Gemma 4
        public const string Gemma4_E4b = "gemma4:e4b";
        public const string Gemma4_26b = "gemma4:26b";

        // Qwen 3
        public const string Qwen3_8b = "qwen3:8b";
        public const string Qwen3_14b = "qwen3:14b";
        public const string Qwen3_30b = "qwen3:30b";

        // Qwen 3.5
        public const string Qwen3_5_9b = "qwen3.5:9b";
        public const string Qwen3_5_27b = "qwen3.5:27b";

        // Qwen 3.6
        public const string Qwen3_6_27b = "qwen3.6:27b";
        public const string Qwen3_6_35b = "qwen3.6:35b";

        // Qwen 3 Coder
        public const string Qwen3Coder_8b = "qwen3-coder:8b";
        public const string Qwen3Coder_30b = "qwen3-coder:30b";

        // DeepSeek R1
        public const string DeepSeekR1_7b = "deepseek-r1:7b";
        public const string DeepSeekR1_14b = "deepseek-r1:14b";

        // Microsoft Phi
        public const string Phi4_14b = "phi4:14b";

        // Mistral
        public const string Mistral_7b = "mistral:7b";
    }

    public static class Vertex
    {
        public const string Gemini2_5Pro = "google/gemini-2.5-pro";
        public const string Gemini2_5Flash = "google/gemini-2.5-flash";
        public const string Veo2_0_Generate = "google/veo-2.0-generate-001";
        public const string Imagen4_0_Generate = "google/imagen-4.0-generate-001";
    }

    public static class Local
    {
        // Gemma
        public const string Gemma2_2b = "gemma2-2b";
        public const string Gemma3_4b = "gemma3-4b";
        public const string Gemma3_12b = "gemma3-12b";
        public const string Gemma3n_e4b = "gemma3n-e4b";

        // Llama
        public const string Llama3_2_3b = "llama3.2-3b";
        public const string Llama3_1_8b = "llama3.1-8b";
        public const string Llava_7b = "llava-7b";
        public const string Llava16Mistral_7b = "llava-1.6-mistral-7b";

        // Hermes
        public const string Hermes3_3b = "hermes3-3b";
        public const string Hermes3_8b = "hermes3-8b";

        // Qwen
        public const string Qwen2_5_0_5b = "qwen2.5-0.5b";
        public const string Qwen2_5_Coder_3b = "qwen2.5-coder-3b";
        public const string Qwen2_5_Coder_7b = "qwen2.5-coder-7b";
        public const string Qwen2_5_Coder_14b = "qwen2.5-coder-14b";
        public const string Qwen3_8b = "qwen3-8b";
        public const string Qwen3_14b = "qwen3-14b";
        public const string QwQ_7b = "qwq-7b";

        // DeepSeek
        public const string DeepSeekR1_8b = "deepseekr1-8b";
        public const string DeepSeekR1_1_5b = "deepseekr1-1.5b";

        // Phi
        public const string Phi3_5_3b = "phi3.5-3b";
        public const string Phi4_4b = "phi4-4b";

        // Other
        public const string Lfm2_1_2b = "lfm2-1.2b";
        public const string Minicpm4_8b = "minicpm4-8b";
        public const string Mistral3_2_24b = "mistral-3.2-24b";
        public const string Webgen_4b = "webgen-4b";
        public const string Bielik2_5_11b = "bielik-2.5-11b";
        public const string OlympicCoder_7b = "olympiccoder-7b";
        public const string Yi_6b = "yi-6b";
        public const string Smollm2_0_1b = "smollm2-0.1b";
        public const string Olmo2_7b = "olmo2-7b";

        // Embedding
        public const string NomicEmbedding = "nomic-embedding";
        public const string MxbaiEmbedding = "mxbai-embedding";

        // TTS
        public const string Kokoro82m = "kokoro-82m";

        // Image Generation
        public const string StableDiffusion1_5 = "stable-diffusion-1.5";
        public const string Flux1Shnell = "FLUX.1_Shnell";
        public const string QwenImage = "qwen-image";
    }
}
