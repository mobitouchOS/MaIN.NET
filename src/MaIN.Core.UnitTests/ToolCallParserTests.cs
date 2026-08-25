using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.Core.UnitTests;

public class ToolCallParserTests
{
    [Fact]
    public void ParseToolCalls_EmptyResponse_IsNotTreatedAsInvalidJson()
    {
        var result = ToolCallParser.ParseToolCalls(string.Empty, ToolFormatDetector.ToolCallFormat.Granite);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(ToolFormatDetector.ToolCallFormat.Granite)]
    [InlineData(ToolFormatDetector.ToolCallFormat.Llama3)]
    [InlineData(ToolFormatDetector.ToolCallFormat.MistralV3)]
    [InlineData(ToolFormatDetector.ToolCallFormat.Phi3)]
    [InlineData(ToolFormatDetector.ToolCallFormat.Qwen3Xml)]
    [InlineData(ToolFormatDetector.ToolCallFormat.HermesJson)]
    public void ParseToolCalls_PlainAnswerWithIncidentalBraces_IsNotTreatedAsFailedToolCall(
        ToolFormatDetector.ToolCallFormat format)
    {
        // A genuine final answer with no tool-call tag at all -- the stray {} is just prose,
        // not a malformed tool call, and must not send the model into a bogus "fix your JSON" retry.
        var answer = "The result (temperature {23} degrees) looks correct based on the search.";

        var result = ToolCallParser.ParseToolCalls(answer, format);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ParseToolCalls_MalformedGraniteToolCallTag_IsStillReportedAsCorrectableError()
    {
        // The model DID attempt a Granite tool call (tag present) but the JSON inside is broken --
        // this is the legitimate self-correction case and must keep surfacing an error.
        var answer = "<tool_call>{\"name\": \"web_search\", \"arguments\": {\"query\": }}</tool_call>";

        var result = ToolCallParser.ParseToolCalls(answer, ToolFormatDetector.ToolCallFormat.Granite);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseToolCalls_ValidGraniteToolCall_StillSucceeds()
    {
        var answer = "<tool_call>{\"name\": \"web_search\", \"arguments\": {\"query\": \"weather\"}}</tool_call>";

        var result = ToolCallParser.ParseToolCalls(answer, ToolFormatDetector.ToolCallFormat.Granite);

        Assert.True(result.IsSuccess);
        Assert.Equal("web_search", result.ToolCalls![0].Function.Name);
    }

    [Fact]
    public void ParseToolCalls_ToolCallJsonWithoutWrapperTag_StillSucceeds()
    {
        // Small models don't always reproduce the exact wrapper tag, but still emit a genuine,
        // recognizably tool-shaped JSON body -- this must still be executed as a real tool call,
        // not silently dropped as "just a plain answer".
        var answer = "{\"name\": \"web_search\", \"arguments\": {\"query\": \"weather\"}}";

        var result = ToolCallParser.ParseToolCalls(answer, ToolFormatDetector.ToolCallFormat.Granite);

        Assert.True(result.IsSuccess);
        Assert.Equal("web_search", result.ToolCalls![0].Function.Name);
    }

    [Fact]
    public void ParseToolCalls_HermesCalculatorAnswerWithParens_IsNotTreatedAsFailedToolCall()
    {
        // Repro: Hermes 8b answering "Policz (48372 + 91847) * 3 - 128" -- no tool_call tag, just
        // parens (not even braces). Must not be misread as a broken tool-call attempt.
        var answer = "The result of (48372 + 91847) * 3 - 128 is 420429.";

        var result = ToolCallParser.ParseToolCalls(answer, ToolFormatDetector.ToolCallFormat.HermesJson);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }
}
