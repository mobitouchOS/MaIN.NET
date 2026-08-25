namespace MaIN.InferPage.Services;

public sealed record McpServerCatalogEntry(
    string Id,
    string Name,
    string Command,
    List<string> Arguments,
    Dictionary<string, string> EnvironmentVariables);
