namespace MaIN.InferPage.Endpoints;

public static class OpenAiApiKeyAuth
{
    private const string BearerPrefix = "Bearer ";

    public static bool IsAuthorized(string? authorizationHeader, string? requiredApiKey, out OpenAiErrorResponse? error)
    {
        if (string.IsNullOrEmpty(requiredApiKey))
        {
            error = null;
            return true;
        }

        var providedKey = authorizationHeader is not null &&
                           authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[BearerPrefix.Length..].Trim()
            : null;

        if (providedKey is not null && string.Equals(providedKey, requiredApiKey, StringComparison.Ordinal))
        {
            error = null;
            return true;
        }

        error = new OpenAiErrorResponse
        {
            Error = new OpenAiError
            {
                Message = "Incorrect API key provided.",
                Type = "invalid_request_error",
                Code = "invalid_api_key"
            }
        };
        return false;
    }
}
