using MaIN.InferPage.Services;

namespace MaIN.InferPage.UnitTests;

public class McpServerCatalogServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "mcp-catalog-tests-" + Guid.NewGuid());

    private McpServerCatalogService CreateService() => new(_tempDir);

    [Fact]
    public async Task GetAllAsync_returns_empty_list_when_no_entries_exist()
    {
        var service = CreateService();

        var result = await service.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAsync_persists_and_is_returned_by_GetAllAsync()
    {
        var service = CreateService();

        var created = await service.CreateAsync("filesystem", "npx", ["-y", "@modelcontextprotocol/server-filesystem"], new Dictionary<string, string> { ["TOKEN"] = "abc" });

        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        Assert.Equal("filesystem", created.Name);

        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(created.Id, all[0].Id);
        Assert.Equal(["-y", "@modelcontextprotocol/server-filesystem"], all[0].Arguments);
        Assert.Equal("abc", all[0].EnvironmentVariables["TOKEN"]);
    }

    [Fact]
    public async Task CreateAsync_assigns_distinct_ids_across_calls()
    {
        var service = CreateService();

        var first = await service.CreateAsync("one", "cmd1", [], []);
        var second = await service.CreateAsync("two", "cmd2", [], []);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_overwrites_fields_and_keeps_the_same_id()
    {
        var service = CreateService();
        var created = await service.CreateAsync("filesystem", "npx", ["-y"], []);

        var updated = await service.UpdateAsync(created.Id, "filesystem-v2", "uvx", ["run"], new Dictionary<string, string> { ["X"] = "1" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("filesystem-v2", updated.Name);
        Assert.Equal("uvx", updated.Command);

        var reloaded = await service.GetByIdAsync(created.Id);
        Assert.Equal("filesystem-v2", reloaded!.Name);
    }

    [Fact]
    public async Task UpdateAsync_throws_when_entry_does_not_exist()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateAsync("missing-id", "name", "cmd", [], []));
    }

    [Fact]
    public async Task DeleteAsync_removes_the_entry()
    {
        var service = CreateService();
        var created = await service.CreateAsync("filesystem", "npx", [], []);

        await service.DeleteAsync(created.Id);

        Assert.Empty(await service.GetAllAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
