namespace MaIN.InferPage.Services;

public static class McpProvenance
{
    public const string CatalogServerIdKey = "CatalogServerId";
    public const string CatalogServerNameKey = "CatalogServerName";

    public static Dictionary<string, string> BuildProperties(string? catalogServerId, string? catalogServerName)
    {
        if (string.IsNullOrWhiteSpace(catalogServerId))
        {
            return new Dictionary<string, string>();
        }

        return new Dictionary<string, string>
        {
            [CatalogServerIdKey] = catalogServerId,
            [CatalogServerNameKey] = catalogServerName ?? string.Empty
        };
    }
}
