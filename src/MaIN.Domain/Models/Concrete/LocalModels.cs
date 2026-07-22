using MaIN.Domain.Models.Abstract;

namespace MaIN.Domain.Models.Concrete;

// ===== Gemma Family =====

public sealed record Gemma2_2b() : LocalModel(
    Models.Local.Gemma2_2b,
    "Gemma2-2b.gguf",
    new Uri("https://huggingface.co/Inza124/gemma2_2b/resolve/main/gemma2-2b-maIN.gguf?download=true"),
    "Gemma 2B",
    8192,
    "Lightweight 2B model for general-purpose text generation and understanding");

public sealed record Gemma3_4b() : LocalModel(
    Models.Local.Gemma3_4b,
    "Gemma3-4b.gguf",
    new Uri("https://huggingface.co/Inza124/Gemma3-4b/resolve/main/gemma3-4b.gguf?download=true"),
    "Gemma3 4B",
    8192,
    "Balanced 4B model for writing, analysis, and mathematical reasoning"), IVisionModel
{
    // https://huggingface.co/ggml-org/gemma-3-4b-it-GGUF/blob/main/mmproj-model-f16.gguf
    public string MMProjectName => "mmproj-model-gemma3-4b.gguf";
}

public sealed record Gemma3_12b() : LocalModel(
    Models.Local.Gemma3_12b,
    "Gemma3-12b.gguf",
    new Uri("https://huggingface.co/Inza124/Gemma3-12b/resolve/main/gemma3-12b.gguf?download=true"),
    "Gemma3 12B",
    8192,
    "Large 12B model for complex analysis, research, and creative writing"), IVisionModel
{
    public string MMProjectName => "mmproj-model-gemma3-12b.gguf";
}

public sealed record Gemma3n_e4b() : LocalModel(
    Models.Local.Gemma3n_e4b,
    "Gemma3n-e4b.gguf",
    new Uri("https://huggingface.co/Inza124/Gemma-3n-e4b/resolve/main/gemma-3n-e4b.gguf?download=true"),
    "Gemma3n E4B",
    8192,
    "Compact 4B model optimized for efficient reasoning and general-purpose tasks"), IVisionModel
{
    public string MMProjectName => "mmproj-model-gemma3n-e4b.gguf";
}

// ===== Llama Family =====

public sealed record Llama3_2_3b() : LocalModel(
    Models.Local.Llama3_2_3b,
    "Llama3.2-3b.gguf",
    new Uri("https://huggingface.co/Inza124/Llama3.2_3b/resolve/main/Llama3.2-maIN.gguf?download=true"),
    "Llama 3.2 3B",
    8192,
    "Lightweight 3B model for chatbots, content creation, and basic coding");

public sealed record Llama3_2_1b() : LocalModel(
    Models.Local.Llama3_2_1b,
    "Llama3.2-1b.gguf",
    new Uri("https://huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf?download=true"),
    "Llama 3.2 1B Instruct",
    128000,
    "Ultra-lightweight 1B model with native tool calling support for agentic tasks");

public sealed record Llama3_1_8b() : LocalModel(
    Models.Local.Llama3_1_8b,
    "Llama3.1-8b.gguf",
    new Uri("https://huggingface.co/Inza124/Llama3.1_8b/resolve/main/Llama3.1-maIN.gguf?download=true"),
    "Llama 3.1 8B",
    8192,
    "Versatile 8B model for writing, coding, math, and general assistance");

public sealed record Llava_7b() : LocalModel(
    Models.Local.Llava_7b,
    "Llava.gguf",
    new Uri("https://huggingface.co/Inza124/Llava/resolve/main/Llava-maIN.gguf?download=true"),
    "LLaVA 7B",
    4096,
    "Vision-language model for image analysis, OCR, and visual Q&A"), IVisionModel
{
    public string MMProjectName => "mmproj-model-llava-7b.gguf";
}

public sealed record Llava16Mistral_7b() : LocalModel(
    Models.Local.Llava16Mistral_7b,
    "llava-1.6-mistral-7b.gguf",
    new Uri("https://huggingface.co/cjpais/llava-1.6-mistral-7b-gguf/resolve/main/llava-v1.6-mistral-7b.Q3_K_XS.gguf?download=true"),
    "LLaVA 1.6 Mistral 7B",
    4096,
    "Vision-language model for image analysis, OCR, and visual Q&A"), IVisionModel
{
    public string MMProjectName => "mmproj-model-llava16Mistral-7b.gguf";
}

// ===== Hermes Family =====

public sealed record Hermes3_3b() : LocalModel(
    Models.Local.Hermes3_3b,
    "Hermes3-3b.gguf",
    new Uri("https://huggingface.co/Inza124/Hermes3-3b/resolve/main/hermes3-3b.gguf?download=true"),
    "Hermes 3 3B",
    8192,
    "Efficient 3B model for dialogue, roleplay, and conversational AI");

public sealed record Hermes3_8b() : LocalModel(
    Models.Local.Hermes3_8b,
    "Hermes3-8b.gguf",
    new Uri("https://huggingface.co/Inza124/Hermes3_8b/resolve/main/hermes3-8b.gguf?download=true"),
    "Hermes 3 8B",
    8192,
    "Enhanced 8B model for complex dialogue, storytelling, and advice");

// ===== Qwen Family =====

public sealed record Qwen2_5_0_5b() : LocalModel(
    Models.Local.Qwen2_5_0_5b,
    "Qwen2.5-0.5b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen2.5/resolve/main/Qwen2.5-maIN.gguf?download=true"),
    "Qwen 2.5 0.5B",
    4096,
    "Ultra-lightweight 0.5B model for simple text completion and basic tasks");

public sealed record Qwen2_5_7b() : LocalModel(
    Models.Local.Qwen2_5_7b,
    "Qwen2.5-7b.gguf",
    new Uri("https://huggingface.co/Qwen/Qwen2.5-7B-Instruct-GGUF/resolve/main/qwen2.5-7b-instruct-q4_k_m.gguf?download=true"),
    "Qwen 2.5 7B Instruct",
    128000,
    "Best-in-class 7B model for tool calling, function use, and agentic workflows");

public sealed record Qwen2_5_Coder_3b() : LocalModel(
    Models.Local.Qwen2_5_Coder_3b,
    "Qwen2.5-coder-3b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen2.5-Coder-3b/resolve/main/Qwen2.5-coder-3b.gguf?download=true"),
    "Qwen 2.5 Coder 3B",
    8192,
    "Compact 3B model for Python, JavaScript, bug fixing, and code review");

public sealed record Qwen2_5_Coder_7b() : LocalModel(
    Models.Local.Qwen2_5_Coder_7b,
    "Qwen2.5-coder-7b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen2.5-Coder-7b/resolve/main/Qwen2.5-coder-7b.gguf?download=true"),
    "Qwen 2.5 Coder 7B",
    8192,
    "Advanced 7B model for full-stack development, API design, and testing");

public sealed record Qwen2_5_Coder_14b() : LocalModel(
    Models.Local.Qwen2_5_Coder_14b,
    "Qwen2.5-coder-14b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen2.5-Coder-14b/resolve/main/Qwen2.5-coder-14b.gguf?download=true"),
    "Qwen 2.5 Coder 14B",
    8192,
    "Professional 14B model for system design, architecture, and code refactoring");

public sealed record Qwen3_8b() : LocalModel(
    Models.Local.Qwen3_8b,
    "Qwen3-8b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen3-8b/resolve/main/Qwen3-8b.gguf?download=true"),
    "Qwen 3 8B",
    8192,
    "Fast 8B model for multilingual tasks, translation, and logical reasoning"
    ), IReasoningModel
{
    // IReasoningModel implementation
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => ReasoningFunctions.ProcessDeepSeekToken;
    public string? AdditionalPrompt => null;
}

public sealed record Qwen3_14b() : LocalModel(
    Models.Local.Qwen3_14b,
    "Qwen3-14b.gguf",
    new Uri("https://huggingface.co/Inza124/Qwen3-14b/resolve/main/Qwen3-14b.gguf?download=true"),
    "Qwen 3 14B",
    8192,
    "Advanced 14B model for complex reasoning, research, and document analysis"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => ReasoningFunctions.ProcessDeepSeekToken;
    public string? AdditionalPrompt => null;
}

public sealed record QwQ_7b() : LocalModel(
    Models.Local.QwQ_7b,
    "QwQ-7b.gguf",
    new Uri("https://huggingface.co/Inza124/QwQ-7b/resolve/main/qwq-7b.gguf?download=true"),
    "QwQ 7B",
    8192,
    "Reasoning-focused 7B model for step-by-step problem solving and analysis"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => ReasoningFunctions.ProcessQwQ_QwenModToken;
    public string? AdditionalPrompt => "- Output nothing before <think>, enclose all step-by-step reasoning (excluding the final answer) within <think>...</think>, and place the final answer immediately after the closing </think>";
}

public sealed record Qwen3_5_4b() : LocalModel(
    Models.Local.Qwen3_5_4b,
    "Qwen3.5-4B-UD-Q4_K_XL.gguf",
    new Uri("https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/resolve/main/Qwen3.5-4B-UD-Q4_K_XL.gguf?download=true"),
    "Qwen 3.5 4B",
    262144,
    "Latest 4B model from Qwen with native tool calling, multimodal support, and 262K context window");

public sealed record Qwen3_5_9b() : LocalModel(
    Models.Local.Qwen3_5_9b,
    "Qwen3.5-9B-UD-Q4_K_XL.gguf",
    new Uri("https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/resolve/main/Qwen3.5-9B-UD-Q4_K_XL.gguf?download=true"),
    "Qwen 3.5 9B",
    262144,
    "Powerful 9B model with native tool calling, multimodal support, and 262K context window");

// ===== DeepSeek Family =====

public sealed record DeepSeek_R1_8b() : LocalModel(
    Models.Local.DeepSeekR1_8b,
    "DeepSeekR1-8b.gguf",
    new Uri("https://huggingface.co/Inza124/DeepseekR1-8b/resolve/main/DeepSeekR1-8b-maIN.gguf?download=true"),
    "DeepSeek R1 8B",
    8192,
    "Advanced 8B model for math proofs, scientific reasoning, and logical puzzles"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => ReasoningFunctions.ProcessDeepSeekToken;
    public string? AdditionalPrompt => null;
}

public sealed record DeepSeek_R1_1_5b() : LocalModel(
    Models.Local.DeepSeekR1_1_5b,
    "DeepSeekR1-1.5b.gguf",
    new Uri("https://huggingface.co/Inza124/DeepseekR1-1.5b/resolve/main/DeepSeekR1-1.5b.gguf?download=true"),
    "DeepSeek R1 1.5B",
    4096,
    "Compact 1.5B model for basic logic, simple math, and chain-of-thought tasks"), IReasoningModel
{
    public Func<string, ThinkingState, LLMTokenValue>? ReasonFunction => ReasoningFunctions.ProcessDeepSeekToken;
    public string? AdditionalPrompt => null;
}

// ===== Phi Family =====

public sealed record Phi3_5_3b() : LocalModel(
    Models.Local.Phi3_5_3b,
    "Phi-3.5-mini-instruct-Q4_K_M.gguf",
    new Uri("https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q4_K_M.gguf?download=true"),
    "Phi 3.5 Mini Instruct",
    128000,
    "Microsoft's 3.8B model with native tool calling support and 128K context");

public sealed record Phi4_4b() : LocalModel(
    Models.Local.Phi4_4b,
    "phi4-4b.gguf",
    new Uri("https://huggingface.co/Inza124/Phi4-4b/resolve/main/phi4-4b.gguf?download=true"),
    "Phi 4 4B",
    4096,
    "Latest 4B model for factual Q&A, safety-focused applications, and education");

// ===== Other Models =====

public sealed record LFM2_1_2b() : LocalModel(
    Models.Local.Lfm2_1_2b,
    "lfm2-1.2b.gguf",
    new Uri("https://huggingface.co/Inza124/Lfm2-1.2b/resolve/main/lfm2-1.2b.gguf?download=true"),
    "LFM2 1.2B",
    4096,
    "Lightweight modern 1.2B model for fast inference and resource-constrained environments");

public sealed record Minicpm4_8b() : LocalModel(
    Models.Local.Minicpm4_8b,
    "Minicpm4-8b.gguf",
    new Uri("https://huggingface.co/Inza124/Minicpm4-8b/resolve/main/MiniCPM4-8b.gguf?download=true"),
    "MiniCPM4 8B",
    8192,
    "Mid-size 8B model balancing performance and efficiency for diverse applications");

public sealed record Mistral_3_2_24b() : LocalModel(
    Models.Local.Mistral3_2_24b,
    "Mistral3.2-24b.gguf",
    new Uri("https://huggingface.co/Inza124/Mistral3.2-24b/resolve/main/Mistral3.2-24b.gguf?download=true"),
    "Mistral 3.2 24B",
    8192,
    "Large 24B model offering advanced reasoning and comprehensive knowledge for complex tasks");

public sealed record Mistral7b() : LocalModel(
    Models.Local.Mistral7b,
    "Mistral-7b-Instruct-v0.3.gguf",
    new Uri("https://huggingface.co/bartowski/Mistral-7B-Instruct-v0.3-GGUF/resolve/main/Mistral-7B-Instruct-v0.3-Q4_K_M.gguf?download=true"),
    "Mistral 7B Instruct v0.3",
    32768,
    "Reliable 7B model with native function calling support for agentic workflows");

public sealed record Nemotron3Nano4b() : LocalModel(
    Models.Local.Nemotron3Nano4b,
    "NVIDIA-Nemotron3-Nano-4B-Q4_K_M.gguf",
    new Uri("https://huggingface.co/nvidia/NVIDIA-Nemotron-3-Nano-4B-GGUF/resolve/main/NVIDIA-Nemotron3-Nano-4B-Q4_K_M.gguf?download=true"),
    "NVIDIA Nemotron 3 Nano 4B",
    262144,
    "NVIDIA's edge-ready 4B model with native tool calling, hybrid Mamba-Transformer architecture, and 262K context");

public sealed record Granite4_1_3b() : LocalModel(
    Models.Local.Granite4_1_3b,
    "granite-4.1-3b-Q4_K_M.gguf",
    new Uri("https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF/resolve/main/granite-4.1-3b-Q4_K_M.gguf?download=true"),
    "IBM Granite 4.1 3B",
    131072,
    "IBM's enterprise-grade 3B model with native tool calling support, designed for RAG and function calling");

public sealed record Webgen_4b() : LocalModel(
    Models.Local.Webgen_4b,
    "webgen-4b.gguf",
    new Uri("https://huggingface.co/Inza124/webgen-4b/resolve/main/Webgen-4b.gguf?download=true"),
    "Webgen 4B",
    8192,
    "Specialized 4B model optimized for web development and code generation tasks");

public sealed record Bielik_2_5_11b() : LocalModel(
    Models.Local.Bielik2_5_11b,
    "Bielik2.5-11b.gguf",
    new Uri("https://huggingface.co/Inza124/Bielik2.5-11b/resolve/main/Bielik2.5-11b.gguf?download=true"),
    "Bielik 2.5 11B",
    8192,
    "Large 11B Polish language model with strong multilingual capabilities and reasoning");

public sealed record OlympicCoder_7b() : LocalModel(
    Models.Local.OlympicCoder_7b,
    "Olympiccoder-7b.gguf",
    new Uri("https://huggingface.co/Inza124/OlympicCoder-7b/resolve/main/OlympicCoder-7b.gguf?download=true"),
    "OlympicCoder 7B",
    8192,
    "Specialized 7B model for algorithms, data structures, and contest programming");

public sealed record Yi_6b() : LocalModel(
    Models.Local.Yi_6b,
    "Yi-6b.gguf",
    new Uri("https://huggingface.co/Inza124/yi-6b/resolve/main/yi-6b.gguf?download=true"),
    "Yi 6B",
    4096,
    "Bilingual 6B model for Chinese-English translation and cultural content");

public sealed record Smollm2_0_1b() : LocalModel(
    Models.Local.Smollm2_0_1b,
    "Smollm2-0.1b.gguf",
    new Uri("https://huggingface.co/Inza124/Smollm2-0.1b/resolve/main/smollm2-0.1b.gguf?download=true"),
    "SmolLM2 0.1B",
    2048,
    "Tiny 0.1B model for keyword extraction, simple classification, and demos");

public sealed record Olmo2_7b() : LocalModel(
    Models.Local.Olmo2_7b,
    "Olmo2-7b.gguf",
    new Uri("https://huggingface.co/Inza124/Olmo2-7b/resolve/main/olmo2-7b.gguf?download=true"),
    "OLMo2 7B",
    8192,
    "Open-source 7B model for research, benchmarking, and academic studies");

// ===== Image Generation =====

public sealed record StableDiffusion1_5() : LocalDiffusionModel(
    Models.Local.StableDiffusion1_5,
    "sd-v1-5-pruned-emaonly-Q8_0.gguf",
    new Uri("https://huggingface.co/second-state/stable-diffusion-v1-5-GGUF/resolve/main/stable-diffusion-v1-5-pruned-emaonly-Q8_0.gguf?download=true"),
    "Stable Diffusion 1.5",
    DiffusionArchitecture.SD1,
    Width: 512,
    Height: 512,
    Steps: 25,
    CfgScale: 7.0f,
    Description: "Small, fast local image generation model - good for CPUs and low-VRAM machines");

public sealed record Flux1Shnell() : LocalDiffusionModel(
    Models.Local.Flux1Shnell,
    "flux1-schnell-Q4_0.gguf",
    new Uri("https://huggingface.co/city96/FLUX.1-schnell-gguf/resolve/main/flux1-schnell-Q4_0.gguf?download=true"),
    "FLUX.1 Schnell",
    DiffusionArchitecture.Flux,
    Width: 768,
    Height: 768,
    Steps: 4,
    CfgScale: 1.0f,
    Vae: new ModelAsset("ae.safetensors", new Uri("https://huggingface.co/Kijai/flux-fp8/resolve/main/flux-vae-bf16.safetensors?download=true")),
    ClipL: new ModelAsset("clip_l.safetensors", new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/clip_l.safetensors?download=true")),
    T5Xxl: new ModelAsset("t5xxl_fp8_e4m3fn.safetensors", new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp8_e4m3fn.safetensors?download=true")),
    Description: "Medium-sized local image generation model - good balance of speed and quality");

public sealed record QwenImage() : LocalDiffusionModel(
    Models.Local.QwenImage,
    "qwen-image-Q4_0.gguf",
    new Uri("https://huggingface.co/city96/Qwen-Image-gguf/resolve/main/qwen-image-Q4_0.gguf?download=true"),
    "Qwen Image",
    DiffusionArchitecture.QwenImage,
    Width: 1024,
    Height: 1024,
    Steps: 20,
    CfgScale: 4.0f,
    Vae: new ModelAsset("qwen_image_vae.safetensors", new Uri("https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/vae/qwen_image_vae.safetensors?download=true")),
    Qwen2VL: new ModelAsset("Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf", new Uri("https://huggingface.co/mradermacher/Qwen2.5-VL-7B-Instruct-GGUF/resolve/main/Qwen2.5-VL-7B-Instruct.Q4_K_M.gguf?download=true")),
    Description: "Largest local image generation model - 20B parameter Qwen-Image model, highest quality, requires significant RAM/VRAM");

// ===== Embedding Model =====

public sealed record Mxbai_Embedding() : LocalModel(
    Models.Local.MxbaiEmbedding,
    "mxbai-embed-large-v1.Q4_K_M.gguf",
    new Uri("https://huggingface.co/ChristianAzinn/mxbai-embed-large-v1-gguf/resolve/main/mxbai-embed-large-v1.Q4_K_M.gguf?download=true"),
    "mxbai-embed-large v1",
    512,
    "Model used to generate embeddings with superior knowledge search recall"), IEmbeddingModel
{
    public int EmbeddingDimension => 1024;
}

public sealed record Nomic_Embedding() : LocalModel(
    Models.Local.NomicEmbedding,
    "nomicv2.gguf",
    new Uri("https://huggingface.co/Inza124/Nomic/resolve/main/nomicv2.gguf?download=true"),
    "Nomic Embedding",
    8192,
    "Model used to generate embeddings");

// ===== TTS Model =====

public sealed record Kokoro_82m() : LocalModel(
    Models.Local.Kokoro82m,
    "kokoro.onnx",
    new Uri("https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/kokoro.onnx"),
    "Kokoro 82M",
    4096,
    "Frontier TTS model for its size of 82 million parameters (text in/audio out)"), ITTSModel;
