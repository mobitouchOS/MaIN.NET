using System.Diagnostics;
using MaIN.InferPage.Services;

namespace MaIN.InferPage.Middlewares;

public sealed class ApiLoggingMiddleware(RequestDelegate next, ApiLogService logService)
{
    private const int ResponseBodyTruncation = 500;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;

        // Skip Razor/Blazor internal requests — only log real API calls
        var pathStr = path.Value ?? string.Empty;
        if (pathStr.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
            pathStr.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            pathStr.StartsWith("/_vs", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            logService.Add(new ApiLogEntry
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Method = method,
                Path = path,
                StatusCode = context.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
