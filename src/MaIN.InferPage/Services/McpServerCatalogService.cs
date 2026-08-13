using System.Text.Json;

namespace MaIN.InferPage.Services;

public sealed class McpServerCatalogService
{
    private readonly string _directoryPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public McpServerCatalogService(string basePath)
    {
        _directoryPath = Path.Combine(basePath, "mcp-servers");
        Directory.CreateDirectory(_directoryPath);
    }

    private string GetFilePath(string id) => Path.Combine(_directoryPath, $"{id}.json");

    public async Task<List<McpServerCatalogEntry>> GetAllAsync()
    {
        var entries = new List<McpServerCatalogEntry>();
        foreach (var file in Directory.GetFiles(_directoryPath, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var entry = JsonSerializer.Deserialize<McpServerCatalogEntry>(json);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        entries = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return entries;
    }

    public async Task<McpServerCatalogEntry> CreateAsync(
        string name, string command, List<string> arguments, Dictionary<string, string> environmentVariables)
    {
        var entry = new McpServerCatalogEntry(Guid.NewGuid().ToString(), name, command, arguments, environmentVariables);
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await File.WriteAllTextAsync(GetFilePath(entry.Id), json);
        return entry;
    }

    public async Task<McpServerCatalogEntry?> GetByIdAsync(string id)
    {
        var filePath = GetFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<McpServerCatalogEntry>(json);
    }

    public async Task<McpServerCatalogEntry> UpdateAsync(
        string id, string name, string command, List<string> arguments, Dictionary<string, string> environmentVariables)
    {
        var filePath = GetFilePath(id);
        if (!File.Exists(filePath))
        {
            throw new KeyNotFoundException($"MCP server catalog entry '{id}' not found.");
        }

        var entry = new McpServerCatalogEntry(id, name, command, arguments, environmentVariables);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(entry, JsonOptions));
        return entry;
    }

    public Task DeleteAsync(string id)
    {
        var filePath = GetFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}
