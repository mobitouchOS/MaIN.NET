namespace MaIN.InferPage.UnitTests;

public class UtilsSanitizeModelFileNameTests
{
    [Fact]
    public void SanitizeModelFileName_AppendsGgufExtension_WhenMissing()
    {
        var result = Utils.SanitizeModelFileName("my-custom-model");

        Assert.Equal("my-custom-model.gguf", result);
    }

    [Fact]
    public void SanitizeModelFileName_DoesNotDoubleAppend_WhenAlreadyGguf()
    {
        var result = Utils.SanitizeModelFileName("already-named.gguf");

        Assert.Equal("already-named.gguf", result);
    }

    [Fact]
    public void SanitizeModelFileName_ReplacesInvalidFileNameCharacters()
    {
        var result = Utils.SanitizeModelFileName("org/model/name");

        Assert.DoesNotContain('/', result);
        Assert.EndsWith(".gguf", result);
    }

    [Fact]
    public void SanitizeModelFileName_FallsBackToDefaultName_WhenResultWouldBeEmpty()
    {
        var result = Utils.SanitizeModelFileName("   ");

        Assert.Equal("custom-model.gguf", result);
    }

    [Fact]
    public void SanitizeModelFileName_ReturnsDefaultName_WhenNull()
    {
        var result = Utils.SanitizeModelFileName(null);

        Assert.Equal("custom-model.gguf", result);
    }
}
