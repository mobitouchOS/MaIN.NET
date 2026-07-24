using MaIN.Domain.Models.Abstract;

namespace MaIN.Services.Services.LLMService.Utils;

/// <summary>
/// Detects the native tool calling format used by a given local model.
/// Different model families use different formats that they were trained on:
/// - HermesJson: Qwen2.5, Hermes, Gemma, DeepSeek, EXAONE, and most open models
/// - Granite: Qwen3 Instruct, IBM Granite — <tool_call>{"name":..., "arguments":...}</tool_call>
/// - Qwen3Xml: Qwen3.5, Qwen3-Coder — <tool_call>&lt;function=name&gt;&lt;parameter=k&gt;v&lt;/parameter&gt;&lt;/function&gt;</tool_call>
/// - Llama3: Llama 3.1+, LLaVA (Llama-based) — &lt;|python_tag|&gt;{...} or &lt;functioncall&gt;...
/// - MistralV3: Mistral v0.3+, Magistral, Devstral — [TOOL_CALLS][...]
/// - Phi3: Phi-3.5 / Phi-4 — &lt;|tool_call|&gt;...&lt;|/tool_call|&gt;
/// </summary>
public static class ToolFormatDetector
{
    public enum ToolCallFormat
    {
        /// <summary>Default Hermes/Qwen2.5 format: <tool_call>{"tool_calls": [{"id":..., "type":..., "function": {"name":..., "arguments":...}}]}</tool_call></summary>
        HermesJson,
        /// <summary>Qwen3 Instruct / IBM Granite format: <tool_call>{"name":..., "arguments":...}</tool_call> (no array wrapper)</summary>
        Granite,
        /// <summary>Qwen3.5 / Qwen3-Coder XML format: <tool_call>&lt;function=name&gt;&lt;parameter=k&gt;v&lt;/parameter&gt;&lt;/function&gt;</tool_call></summary>
        Qwen3Xml,
        /// <summary>Llama 3.1+ format: &lt;|python_tag|&gt;{...} or &lt;functioncall&gt;{"name":..., "arguments":"{...}"}</summary>
        Llama3,
        /// <summary>Mistral v0.3+ format: [TOOL_CALLS][{"name":..., "arguments":...}]</summary>
        MistralV3,
        /// <summary>Phi-3.5 format: &lt;|tool_call|&gt;...&lt;|/tool_call|&gt;</summary>
        Phi3,
    }

    /// <summary>
    /// Detects the tool calling format based on the model file name or id.
    /// </summary>
    public static ToolCallFormat DetectFormat(LocalModel model)
    {
        var fileName = model.FileName?.ToLowerInvariant() ?? string.Empty;
        var id = model.Id?.ToLowerInvariant() ?? string.Empty;

        // ── Qwen3.5 / Qwen3-Coder: XML function/parameter format ───
        // Must be checked BEFORE Qwen3 Instruct since ids contain "qwen3"
        if (fileName.Contains("qwen3.5") || id.Contains("qwen3.5") ||
            fileName.Contains("qwen3-coder") || id.Contains("qwen3-coder") ||
            fileName.Contains("qwen3_coder") || id.Contains("qwen3_coder"))
            return ToolCallFormat.Qwen3Xml;

        // ── Qwen3 Instruct: JSON in <tool_call> (same as Granite) ────
        // Qwen3 Instruct outputs: <tool_call>{"name":..., "arguments":...}</tool_call>
        if (fileName.Contains("qwen3") || id.Contains("qwen3"))
            return ToolCallFormat.Granite;

        // ── IBM Granite ──────────────────────────────────────────────
        if (fileName.Contains("granite") || id.Contains("granite"))
            return ToolCallFormat.Granite;

        // ── Phi-3.5 / Phi-4 ─────────────────────────────────────────
        if (fileName.Contains("phi-3") || fileName.Contains("phi3") ||
            fileName.Contains("phi-4") || fileName.Contains("phi4") ||
            id.Contains("phi-3") || id.Contains("phi3") ||
            id.Contains("phi-4") || id.Contains("phi4"))
            return ToolCallFormat.Phi3;

        // ── Llama 3.1+ and LLaVA (Llama-based vision models) ────────
        if (fileName.Contains("llama-3") || fileName.Contains("llama3") ||
            fileName.Contains("llama_3") ||
            id.Contains("llama-3") || id.Contains("llama3") ||
            fileName.Contains("llava") || id.Contains("llava"))
            return ToolCallFormat.Llama3;

        // ── Mistral v0.3+, Magistral, Devstral ──────────────────────
        if (fileName.Contains("mistral") || id.Contains("mistral") ||
            fileName.Contains("magistral") || id.Contains("magistral") ||
            fileName.Contains("devstral") || id.Contains("devstral"))
            return ToolCallFormat.MistralV3;

        // ── HermesJson: Qwen2.5, Hermes, Nemotron, Gemma, ───────────
        //    DeepSeek, EXAONE, MiniCPM, Bielik, OLMo, and others
        if (fileName.Contains("qwen2") || id.Contains("qwen2") ||
            fileName.Contains("qwq") || id.Contains("qwq") ||
            fileName.Contains("hermes") || id.Contains("hermes") ||
            fileName.Contains("nemotron") || id.Contains("nemotron") ||
            fileName.Contains("gemma") || id.Contains("gemma") ||
            fileName.Contains("deepseek") || id.Contains("deepseek") ||
            fileName.Contains("exaone") || id.Contains("exaone") ||
            fileName.Contains("minicpm") || id.Contains("minicpm") ||
            fileName.Contains("bielik") || id.Contains("bielik") ||
            fileName.Contains("olmo") || id.Contains("olmo") ||
            fileName.Contains("lfm") || id.Contains("lfm") ||
            fileName.Contains("olympiccoder") || id.Contains("olympiccoder"))
            return ToolCallFormat.HermesJson;

        // Default to Hermes format which is the most widely supported
        return ToolCallFormat.HermesJson;
    }
}
