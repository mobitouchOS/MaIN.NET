using MaIN.InferPage.Endpoints;

namespace MaIN.InferPage.UnitTests;

public class OpenAiApiKeyAuthTests
{
    [Fact]
    public void IsAuthorized_ReturnsTrue_WhenNoApiKeyIsConfigured()
    {
        var result = OpenAiApiKeyAuth.IsAuthorized(authorizationHeader: null, requiredApiKey: null, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void IsAuthorized_ReturnsTrue_WhenBearerTokenMatches()
    {
        var result = OpenAiApiKeyAuth.IsAuthorized("Bearer secret-key", "secret-key", out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void IsAuthorized_ReturnsFalse_WhenHeaderMissing()
    {
        var result = OpenAiApiKeyAuth.IsAuthorized(authorizationHeader: null, requiredApiKey: "secret-key", out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal("invalid_api_key", error!.Error.Code);
    }

    [Fact]
    public void IsAuthorized_ReturnsFalse_WhenTokenDoesNotMatch()
    {
        var result = OpenAiApiKeyAuth.IsAuthorized("Bearer wrong-key", "secret-key", out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void IsAuthorized_ReturnsFalse_WhenHeaderIsNotBearerScheme()
    {
        var result = OpenAiApiKeyAuth.IsAuthorized("Basic secret-key", "secret-key", out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }
}
