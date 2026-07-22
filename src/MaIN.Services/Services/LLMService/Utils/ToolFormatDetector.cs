using MaIN.Domain.Models.Abstract;

namespace MaIN.Services.Services.LLMService.Utils;

/// <summary>
/// Detects the native tool calling format used by a given local model.
/// Different model families use different formats that they were trained on:
/// - Qwen2.5/Hermes: &lt;tool_call&gt;...&lt;/tool_call&gt; with plain JSON
/// - Llama 3.1+: &lt;|python_tag|&gt;{...} or &lt;functioncall&gt;...
/// - Mistral v0.3+: [TOOL_CALLS][...]
/// - Phi-3.5: &lt;|tool_call|&gt;...&lt;|/tool_call|&gt;
/// </summary>
public static class ToolFormatDetector
{
    public enum ToolCallFormat
    {
        /// <summary>Default Hermes/Qwen2.5 format: <tool_call>{"tool_calls": [{"id":..., "type":..., "function": {"name":..., "arguments":...}}]}</tool_call></summary>
        HermesJson,
        /// <summary>IBM Granite format: <tool_call>{"name":..., "arguments":...}</tool_call> (no array wrapper)</summary>
        Granite,
        /// <summary>Llama 3.1+ format: <|python_tag|>{...} or <functioncall>{"name":..., "arguments":"{...}"}</summary>
        Llama3,
        /// <summary>Mistral v0.3+ format: [TOOL_CALLS][{"name":..., "arguments":...}]</summary>
        MistralV3,
        /// <summary>Phi-3.5 format: <|tool_call|>{...}<|/tool_call|></summary>
        Phi3,
    }

    /// <summary>
    /// Detects the tool calling format based on the model file name or id.
    /// </summary>
    public static ToolCallFormat DetectFormat(LocalModel model)
    {
        var fileName = model.FileName.ToLowerInvariant();
        var id = model.Id.ToLowerInvariant();

        // IBM Granite - uses simple <tool_call>{"name":..., "arguments":...}</tool_call> format
        if (fileName.Contains("granite") || id.Contains("granite"))
        {
            return ToolCallFormat.Granite;
        }

        // Qwen2.5, Qwen3, Qwen3.5, Hermes, NousResearch hermes models - use Hermes JSON format
        if (fileName.Contains("qwen2.5") || fileName.Contains("qwen2_5") ||
            fileName.Contains("qwen3.5") || fileName.Contains("qwen3_5") ||
            id.Contains("qwen2.5") || id.Contains("qwen2_5") ||
            id.Contains("qwen3.5") || id.Contains("qwen3_5") ||
            fileName.Contains("hermes") || id.Contains("hermes"))
        {
            return ToolCallFormat.HermesJson;
        }

        // NVIDIA Nemotron - uses Hermes-style tool calling format
        if (fileName.Contains("nemotron") || id.Contains("nemotron"))
        {
            return ToolCallFormat.HermesJson;
        }

        // Llama 3.1, 3.2, 3.3 - use Llama 3 native format
        if (fileName.Contains("llama-3") || fileName.Contains("llama3") ||
            id.Contains("llama-3") || id.Contains("llama3") ||
            fileName.Contains("llama_3"))
        {
            return ToolCallFormat.Llama3;
        }

        // Mistral v0.3+ and newer - use [TOOL_CALLS] format
        if (fileName.Contains("mistral") || id.Contains("mistral"))
        {
            return ToolCallFormat.MistralV3;
        }

        // Phi-3.5 / Phi-4 - use <|tool_call|> format
        if (fileName.Contains("phi-3") || fileName.Contains("phi3") ||
            fileName.Contains("phi-4") || fileName.Contains("phi4") ||
            id.Contains("phi-3") || id.Contains("phi3") ||
            id.Contains("phi-4") || id.Contains("phi4"))
        {
            return ToolCallFormat.Phi3;
        }

        // Default to Hermes format which is the most widely supported
        return ToolCallFormat.HermesJson;
    }
}
