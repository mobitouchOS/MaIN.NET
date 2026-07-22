using System.Diagnostics;
using MaIN.InferPage.Services;

namespace MaIN.InferPage.Middlewares;

public sealed class ApiLoggingMiddleware(RequestDelegate next, ApiLogService logService)
{
    private const int BodyTruncation = 1000;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only log real API calls under /v1/
        if (!path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;

        // Capture request body for POST/PUT/PATCH
        string? requestBody = null;
        if (context.Request.Body.CanSeek || context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Capture response body
        var originalBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            context.Response.Body = originalBody;
            responseBuffer.Position = 0;

            string? responseBody = null;
            if (responseBuffer.Length > 0)
            {
                using var reader = new StreamReader(responseBuffer);
                responseBody = await reader.ReadToEndAsync();
                responseBuffer.Position = 0;
            }

            await responseBuffer.CopyToAsync(originalBody);

            logService.Add(new ApiLogEntry
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Method = method,
                Path = path,
                StatusCode = context.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                RequestBody = Truncate(requestBody, BodyTruncation),
                ResponseBody = Truncate(responseBody, BodyTruncation)
            });
        }
    }

    private static string? Truncate(string? value, int maxLen) =>
        value is null ? null : value.Length <= maxLen ? value : value[..maxLen] + "...";
}
