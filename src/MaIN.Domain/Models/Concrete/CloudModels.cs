using MaIN.Domain.Configuration;
using MaIN.Domain.Models.Abstract;

namespace MaIN.Domain.Models.Concrete;

// ===== OpenAI Models =====

public sealed record Gpt4o() : CloudModel(
    Models.OpenAi.Gpt4o,
    BackendType.OpenAi,
    "GPT-4o",
    128000,
    "OpenAI's flagship multimodal model with strong reasoning and vision"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt4_1() : CloudModel(
    Models.OpenAi.Gpt4_1,
    BackendType.OpenAi,
    "GPT-4.1",
    1000000,
    "OpenAI's most capable GPT model with 1M token context window"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record O3() : CloudModel(
    Models.OpenAi.O3,
    BackendType.OpenAi,
    "o3",
    200000,
    "OpenAI's most powerful reasoning model for complex multi-step problems"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record O3Mini() : CloudModel(
    Models.OpenAi.O3Mini,
    BackendType.OpenAi,
    "o3 Mini",
    200000,
    "Compact and efficient OpenAI reasoning model"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record O4Mini() : CloudModel(
    Models.OpenAi.O4Mini,
    BackendType.OpenAi,
    "o4 Mini",
    200000,
    "OpenAI's fast reasoning model optimised for agentic tool use"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record Gpt4oMini() : CloudModel(
    Models.OpenAi.Gpt4oMini,
    BackendType.OpenAi,
    "GPT-4o Mini",
    ModelDefaults.DefaultMaxContextWindow,
    "Fast and affordable OpenAI model for everyday tasks"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt4_1Mini() : CloudModel(
    Models.OpenAi.Gpt4_1Mini,
    BackendType.OpenAi,
    "GPT-4.1 Mini",
    ModelDefaults.DefaultMaxContextWindow,
    "Updated mini model with improved capabilities"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5() : CloudModel(
    Models.OpenAi.Gpt5,
    BackendType.OpenAi,
    "GPT-5",
    1000000,
    "OpenAI's most capable model with native reasoning and multimodal understanding"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record Gpt5Mini() : CloudModel(
    Models.OpenAi.Gpt5Mini,
    BackendType.OpenAi,
    "GPT-5 Mini",
    1000000,
    "Faster and more affordable GPT-5 variant"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5Nano() : CloudModel(
    Models.OpenAi.Gpt5Nano,
    BackendType.OpenAi,
    "GPT-5 Nano",
    ModelDefaults.DefaultMaxContextWindow,
    "Next generation OpenAI nano model"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_1() : CloudModel(
    Models.OpenAi.Gpt5_1,
    BackendType.OpenAi,
    "GPT-5.1",
    1000000,
    "OpenAI's flagship model for coding and agentic tasks with configurable reasoning"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record Gpt5_2() : CloudModel(
    Models.OpenAi.Gpt5_2,
    BackendType.OpenAi,
    "GPT-5.2",
    1000000,
    "OpenAI frontier model for complex professional work with long-context reasoning"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_4() : CloudModel(
    Models.OpenAi.Gpt5_4,
    BackendType.OpenAi,
    "GPT-5.4",
    1000000,
    "OpenAI frontier model producing smarter and more precise responses"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_4Mini() : CloudModel(
    Models.OpenAi.Gpt5_4Mini,
    BackendType.OpenAi,
    "GPT-5.4 Mini",
    1000000,
    "Faster, more efficient GPT-5.4 variant designed for high-volume workloads"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_4Nano() : CloudModel(
    Models.OpenAi.Gpt5_4Nano,
    BackendType.OpenAi,
    "GPT-5.4 Nano",
    1000000,
    "Ultra-fast GPT-5.4 variant optimised for classification, extraction, and sub-agents"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_5() : CloudModel(
    Models.OpenAi.Gpt5_5,
    BackendType.OpenAi,
    "GPT-5.5",
    1000000,
    "OpenAI's latest flagship model for the most complex professional and agentic work"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gpt5_5Pro() : CloudModel(
    Models.OpenAi.Gpt5_5Pro,
    BackendType.OpenAi,
    "GPT-5.5 Pro",
    1000000,
    "GPT-5.5 variant that uses extended compute to think harder for maximum precision"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record CodexMini() : CloudModel(
    Models.OpenAi.CodexMini,
    BackendType.OpenAi,
    "Codex Mini",
    200000,
    "OpenAI's Codex model optimised for agentic code generation and software engineering"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record DallE3() : CloudModel(
    Models.OpenAi.DallE3,
    BackendType.OpenAi,
    "DALL-E 3",
    4000,
    "Advanced image generation model from OpenAI"), IImageGenerationModel;

public sealed record GptImage1() : CloudModel(
    Models.OpenAi.GptImage1,
    BackendType.OpenAi,
    "GPT Image 1",
    4000,
    "OpenAI's latest image generation model"), IImageGenerationModel;

// ===== Anthropic Models =====

public sealed record ClaudeOpus4_7() : CloudModel(
    Models.Anthropic.ClaudeOpus4_7,
    BackendType.Anthropic,
    "Claude Opus 4.7",
    200000,
    "Anthropic's most capable model with extended thinking and superior reasoning"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record ClaudeSonnet4_6() : CloudModel(
    Models.Anthropic.ClaudeSonnet4_6,
    BackendType.Anthropic,
    "Claude Sonnet 4.6",
    200000,
    "Anthropic's balanced model with strong performance and speed"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record ClaudeHaiku4_5() : CloudModel(
    Models.Anthropic.ClaudeHaiku4_5,
    BackendType.Anthropic,
    "Claude Haiku 4.5",
    200000,
    "Anthropic's fastest and most compact model for everyday tasks"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record ClaudeSonnet4() : CloudModel(
    Models.Anthropic.ClaudeSonnet4,
    BackendType.Anthropic,
    "Claude Sonnet 4",
    200000,
    "Latest Claude model with enhanced reasoning capabilities"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record ClaudeSonnet4_5() : CloudModel(
    Models.Anthropic.ClaudeSonnet4_5,
    BackendType.Anthropic,
    "Claude Sonnet 4.5",
    200000,
    "Advanced Claude model with superior performance and extended context"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record ClaudeSonnet5() : CloudModel(
    Models.Anthropic.ClaudeSonnet5,
    BackendType.Anthropic,
    "Claude Sonnet 5",
    200000,
    "Anthropic's latest Sonnet model with enhanced reasoning and multimodal capabilities"), IVisionModel
{
    public string? MMProjectName => null;
}

// ===== Gemini Models =====

public sealed record Gemini3_5Flash() : CloudModel(
    Models.Gemini.Gemini3_5Flash,
    BackendType.Gemini,
    "Gemini 3.5 Flash",
    1000000,
    "Google's latest flagship model for agentic tasks, coding, and multi-step workflows"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gemini3_1FlashLite() : CloudModel(
    Models.Gemini.Gemini3_1FlashLite,
    BackendType.Gemini,
    "Gemini 3.1 Flash Lite",
    1000000,
    "Low-latency, cost-efficient Gemini model for high-volume agentic and extraction workloads"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gemini3_1ProPreview() : CloudModel(
    Models.Gemini.Gemini3_1ProPreview,
    BackendType.Gemini,
    "Gemini 3.1 Pro (Preview)",
    1000000,
    "Google's most capable Pro model with improved thinking and factual consistency"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gemini3_1FlashImagePreview() : CloudModel(
    Models.Gemini.Gemini3_1FlashImagePreview,
    BackendType.Gemini,
    "Gemini 3.1 Flash Image (Preview)",
    4000,
    "Google's latest image generation model via Gemini Flash"), IImageGenerationModel;

public sealed record Gemini2_5Flash() : CloudModel(
    Models.Gemini.Gemini2_5Flash,
    BackendType.Gemini,
    "Gemini 2.5 Flash",
    1000000,
    "Fast and efficient Google Gemini model for quick responses"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gemini2_0Flash() : CloudModel(
    Models.Gemini.Gemini2_0Flash,
    BackendType.Gemini,
    "Gemini 2.0 Flash",
    1000000,
    "Google Gemini 2.0 flash model optimized for speed and efficiency"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Gemini2_5Pro() : CloudModel(
    Models.Gemini.Gemini2_5Pro,
    BackendType.Gemini,
    "Gemini 2.5 Pro",
    1000000,
    "Google's most capable Gemini model"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record GeminiImagen4_0FastGenerate() : CloudModel(
    Models.Gemini.Imagen4_0_FastGenerate,
    BackendType.Gemini,
    "Imagen 4.0 Fast (Gemini)",
    4000,
    "Google's fast image generation model via Gemini API"), IImageGenerationModel;

public sealed record GeminiNanoBanana() : CloudModel(
    Models.Gemini.NanoBanana,
    BackendType.Gemini,
    "Gemini 2.5 Flash Image (NanoBanana)",
    130000,
    "Google’s high-speed, high-fidelity image generation via Gemini API."), IImageGenerationModel;

// ===== Vertex AI Models =====

public sealed record VertexGemini2_5Pro() : CloudModel(
    Models.Vertex.Gemini2_5Pro,
    BackendType.Vertex,
    "Gemini 2.5 Pro (Vertex)",
    1000000,
    "Fast and efficient Gemini model served via Vertex AI"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record VertexGemini2_5Flash() : CloudModel(
    Models.Vertex.Gemini2_5Flash,
    BackendType.Vertex,
    "Gemini 2.5 Flash (Vertex)",
    1000000,
    "Fast and efficient Gemini model served via Vertex AI"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record VertexVeo2_0Generate() : CloudModel(
    Models.Vertex.Veo2_0_Generate,
    BackendType.Vertex,
    "Veo 2.0 Generate",
    4000,
    "Google's video generation model available through Vertex AI"), IImageGenerationModel;

public sealed record VertexImagen4_0Generate() : CloudModel(
    Models.Vertex.Imagen4_0_Generate,
    BackendType.Vertex,
    "Imagen 4.0 (Vertex)",
    4000,
    "Google's latest image generation model available through Vertex AI"), IImageGenerationModel;

// ===== xAI Models =====

public sealed record Grok4_20Reasoning() : CloudModel(
    Models.Xai.Grok4_20Reasoning,
    BackendType.Xai,
    "Grok 4.20 reasoning",
    2_000_000,
    "A xai flagship model, offering fast, agentic tool use with low hallucination and strong prompt adherence for precise, reliable responses."), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record Grok4_20NonReasoning() : CloudModel(
    Models.Xai.Grok4_20NonReasoning,
    BackendType.Xai,
    "Grok 4.20 non reasoning",
    2_000_000,
    "A xai flagship model, offering fast, agentic tool use with low hallucination and strong prompt adherence for precise, reliable responses."), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Grok4_1FastReasoning() : CloudModel(
    Models.Xai.Grok4_1FastReasoning,
    BackendType.Xai,
    "Grok 4.1 fast reasoning",
    2_000_000,
    "A xai multimodal model optimized specifically for high-performance agentic tool calling"), IVisionModel, IReasoningModel
{
    public string? MMProjectName => null;
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record Grok4_1Fast() : CloudModel(
    Models.Xai.Grok4_1FastNonReasoning,
    BackendType.Xai,
    "Grok 4.1 fast",
    2_000_000,
    "A xai multimodal model optimized specifically for high-performance agentic tool calling"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record Grok3Beta() : CloudModel(
    Models.Xai.Grok3Beta,
    BackendType.Xai,
    "Grok 3 Beta",
    ModelDefaults.DefaultMaxContextWindow,
    "xAI latest Grok model in beta testing phase"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record GrokImage() : CloudModel(
    Models.Xai.GrokImage,
    BackendType.Xai,
    "Grok 2 Image",
    4000,
    "xAI image generation model"), IImageGenerationModel;

public sealed record GrokImagineImage() : CloudModel(
    Models.Xai.GrokImagineImage,
    BackendType.Xai,
    "Grok Imagine Image",
    4000,
    "xAI image generation model"), IImageGenerationModel, IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record GrokImagineImagePro() : CloudModel(
    Models.Xai.GrokImagineImagePro,
    BackendType.Xai,
    "Grok Imagine Image Pro",
    4000,
    "xAI image generation model"), IImageGenerationModel, IVisionModel
{
    public string? MMProjectName => null;
}

// ===== GroqCloud Models =====

public sealed record Llama4Scout17b() : CloudModel(
    Models.Groq.Llama4Scout17b,
    BackendType.GroqCloud,
    "Llama 4 Scout 17B (Groq)",
    131072,
    "Meta's MoE model with 17B active parameters, vision support, and fast inference on Groq"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record GroqCompound() : CloudModel(
    Models.Groq.Compound,
    BackendType.GroqCloud,
    "Groq Compound",
    131072,
    "Groq's AI system with built-in web search and code execution for complex agentic tasks");

public sealed record GroqCompoundMini() : CloudModel(
    Models.Groq.CompoundMini,
    BackendType.GroqCloud,
    "Groq Compound Mini",
    131072,
    "Lightweight Groq compound system for fast agentic workflows with tool use");

public sealed record Qwen3_32b() : CloudModel(
    Models.Groq.Qwen3_32b,
    BackendType.GroqCloud,
    "Qwen 3 32B (Groq)",
    128000,
    "Alibaba's Qwen 3 32B model running on Groq infrastructure");

public sealed record Llama3_1_8bInstant() : CloudModel(
    Models.Groq.Llama3_1_8b,
    BackendType.GroqCloud,
    "Llama 3.1 8B Instant",
    8192,
    "Meta Llama 3.1 8B model optimized for fast inference on Groq hardware");

public sealed record Llama3_3_70bVersatile() : CloudModel(
    Models.Groq.Llama3_3_70b,
    BackendType.GroqCloud,
    "Llama 3.3 70B Versatile",
    130_000,
    "Meta's efficient, high-performance multilingual language model");

public sealed record GptOss20b() : CloudModel(
    Models.Groq.GptOss20b,
    BackendType.GroqCloud,
    "GPT OSS 20B",
    8192,
    "Open-source 20B parameter GPT model running on Groq infrastructure");

public sealed record GptOss120b() : CloudModel(
    Models.Groq.GptOss120b,
    BackendType.GroqCloud,
    "GPT OSS 120B",
    130_000,
    "Open-source 120B parameter GPT model running on Groq infrastructure");

// ===== DeepSeek Models =====

public sealed record DeepSeekReasoner() : CloudModel(
    Models.DeepSeek.Reasoner,
    BackendType.DeepSeek,
    "DeepSeek Reasoner",
    128_000,
    "DeepSeek reasoning-focused model for complex problem solving"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record DeepSeekChat() : CloudModel(
    Models.DeepSeek.Chat,
    BackendType.DeepSeek,
    "DeepSeek Chat",
    128_000,
    "DeepSeek model for complex problem solving");

// ===== Ollama Models =====

// Llama 4
public sealed record OllamaLlama4Scout() : CloudModel(
    Models.Ollama.Llama4Scout,
    BackendType.Ollama,
    "Llama 4 Scout (Ollama)",
    131072,
    "Meta's MoE model with native multimodal understanding running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record OllamaLlama4Maverick() : CloudModel(
    Models.Ollama.Llama4Maverick,
    BackendType.Ollama,
    "Llama 4 Maverick (Ollama)",
    131072,
    "Meta's larger Llama 4 MoE variant with multimodal capabilities running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

// Gemma 3
public sealed record OllamaGemma3_4b() : CloudModel(
    Models.Ollama.Gemma3_4b,
    BackendType.Ollama,
    "Gemma3 4B (Ollama)",
    128000,
    "Balanced 4B model running on Ollama for writing, analysis, and mathematical reasoning"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record OllamaGemma3_12b() : CloudModel(
    Models.Ollama.Gemma3_12b,
    BackendType.Ollama,
    "Gemma3 12B (Ollama)",
    128000,
    "Google's mid-range Gemma 3 model with strong reasoning running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record OllamaGemma3_27b() : CloudModel(
    Models.Ollama.Gemma3_27b,
    BackendType.Ollama,
    "Gemma3 27B (Ollama)",
    128000,
    "Google's largest Gemma 3 model with frontier-level performance running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

// Gemma 4
public sealed record OllamaGemma4_E4b() : CloudModel(
    Models.Ollama.Gemma4_E4b,
    BackendType.Ollama,
    "Gemma4 E4B (Ollama)",
    128000,
    "Efficient 4B Gemma 4 model for agentic and coding tasks running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record OllamaGemma4_26b() : CloudModel(
    Models.Ollama.Gemma4_26b,
    BackendType.Ollama,
    "Gemma4 26B (Ollama)",
    128000,
    "Google's powerful Gemma 4 26B model for reasoning and multimodal tasks running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

// Qwen 3
public sealed record OllamaQwen3_8b() : CloudModel(
    Models.Ollama.Qwen3_8b,
    BackendType.Ollama,
    "Qwen3 8B (Ollama)",
    128000,
    "Alibaba's Qwen 3 8B model running on Ollama");

public sealed record OllamaQwen3_14b() : CloudModel(
    Models.Ollama.Qwen3_14b,
    BackendType.Ollama,
    "Qwen3 14B (Ollama)",
    128000,
    "Alibaba's Qwen 3 14B model running on Ollama");

public sealed record OllamaQwen3_30b() : CloudModel(
    Models.Ollama.Qwen3_30b,
    BackendType.Ollama,
    "Qwen3 30B (Ollama)",
    128000,
    "Alibaba's Qwen 3 30B MoE model running on Ollama");

// Qwen 3.5
public sealed record OllamaQwen3_5_9b() : CloudModel(
    Models.Ollama.Qwen3_5_9b,
    BackendType.Ollama,
    "Qwen3.5 9B (Ollama)",
    128000,
    "Alibaba's multimodal Qwen 3.5 9B model running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

public sealed record OllamaQwen3_5_27b() : CloudModel(
    Models.Ollama.Qwen3_5_27b,
    BackendType.Ollama,
    "Qwen3.5 27B (Ollama)",
    128000,
    "Alibaba's multimodal Qwen 3.5 27B model running on Ollama"), IVisionModel
{
    public string? MMProjectName => null;
}

// Qwen 3.6
public sealed record OllamaQwen3_6_27b() : CloudModel(
    Models.Ollama.Qwen3_6_27b,
    BackendType.Ollama,
    "Qwen3.6 27B (Ollama)",
    128000,
    "Alibaba's Qwen 3.6 27B with improved agentic coding running on Ollama");

public sealed record OllamaQwen3_6_35b() : CloudModel(
    Models.Ollama.Qwen3_6_35b,
    BackendType.Ollama,
    "Qwen3.6 35B (Ollama)",
    128000,
    "Alibaba's Qwen 3.6 35B with strong coding and reasoning running on Ollama");

// Qwen 3 Coder
public sealed record OllamaQwen3Coder_8b() : CloudModel(
    Models.Ollama.Qwen3Coder_8b,
    BackendType.Ollama,
    "Qwen3-Coder 8B (Ollama)",
    128000,
    "Alibaba's efficient code-focused Qwen 3 model running on Ollama");

public sealed record OllamaQwen3Coder_30b() : CloudModel(
    Models.Ollama.Qwen3Coder_30b,
    BackendType.Ollama,
    "Qwen3-Coder 30B (Ollama)",
    128000,
    "Alibaba's most capable agentic code model running on Ollama");

// DeepSeek R1
public sealed record OllamaDeepSeekR1_7b() : CloudModel(
    Models.Ollama.DeepSeekR1_7b,
    BackendType.Ollama,
    "DeepSeek R1 7B (Ollama)",
    128000,
    "DeepSeek's open reasoning model at 7B running on Ollama"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

public sealed record OllamaDeepSeekR1_14b() : CloudModel(
    Models.Ollama.DeepSeekR1_14b,
    BackendType.Ollama,
    "DeepSeek R1 14B (Ollama)",
    128000,
    "DeepSeek's open reasoning model at 14B running on Ollama"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => null;
    public string? AdditionalPrompt => null;
}

// Microsoft Phi
public sealed record OllamaPhi4_14b() : CloudModel(
    Models.Ollama.Phi4_14b,
    BackendType.Ollama,
    "Phi4 14B (Ollama)",
    16000,
    "Microsoft's state-of-the-art 14B model running on Ollama");

// Mistral
public sealed record OllamaMistral_7b() : CloudModel(
    Models.Ollama.Mistral_7b,
    BackendType.Ollama,
    "Mistral 7B (Ollama)",
    32000,
    "Mistral AI's efficient 7B base model running on Ollama");
