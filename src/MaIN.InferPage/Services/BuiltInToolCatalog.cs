using MaIN.Services.Services.LLMService.Utils;

namespace MaIN.InferPage.Services;

/// <summary>A built-in tool the user can attach to an agent from the UI.</summary>
public sealed record BuiltInToolInfo(string Name, string DisplayName, string Description);

// Derived from HostedToolsResolver so a new library tool surfaces automatically; the map only
// overrides display names that snake_case prettifying gets wrong (acronyms, punctuation).
public static class BuiltInToolCatalog
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNameOverrides = new Dictionary<string, string>
    {
        ["get_current_datetime"] = "Current Date/Time",
        ["http_request"] = "HTTP Request",
        ["rss_feed_reader"] = "RSS Feed Reader",
        ["extract_url_metadata"] = "URL Metadata",
    };

    public static readonly IReadOnlyList<BuiltInToolInfo> All =
        HostedToolsResolver.GetAllBuiltInTools()
            .Where(t => t.Function is not null)
            .Select(t => new BuiltInToolInfo(
                t.Function!.Name,
                DisplayNameOverrides.GetValueOrDefault(t.Function.Name, Prettify(t.Function.Name)),
                t.Function.Description ?? string.Empty))
            .ToList();

    public static bool IsKnown(string name) => All.Any(t => t.Name == name);

    private static string Prettify(string name) =>
        string.Join(' ', name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
