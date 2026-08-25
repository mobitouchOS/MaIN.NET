using MaIN.InferPage.Services;

namespace MaIN.InferPage.UnitTests;

public class McpProvenanceTests
{
    [Fact]
    public void BuildProperties_returns_empty_dict_when_no_catalog_id()
    {
        var result = McpProvenance.BuildProperties(null, "some name");

        Assert.Empty(result);
    }

    [Fact]
    public void BuildProperties_returns_empty_dict_when_catalog_id_is_whitespace()
    {
        var result = McpProvenance.BuildProperties("   ", "some name");

        Assert.Empty(result);
    }

    [Fact]
    public void BuildProperties_stamps_id_and_name_when_catalog_id_present()
    {
        var result = McpProvenance.BuildProperties("abc-123", "Filesystem");

        Assert.Equal("abc-123", result["CatalogServerId"]);
        Assert.Equal("Filesystem", result["CatalogServerName"]);
    }

    [Fact]
    public void BuildProperties_defaults_name_to_empty_string_when_null()
    {
        var result = McpProvenance.BuildProperties("abc-123", null);

        Assert.Equal("", result["CatalogServerName"]);
    }
}
