using System.Collections.Concurrent;

namespace MaIN.InferPage.Services;

public sealed class ApiLogEntry
{
    public required string Id { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public DateTime Timestamp { get; init; }
    public string? RequestBody { get; init; }
    public string? ResponseBody { get; init; }
}

public sealed class ApiLogService
{
    private const int MaxEntries = 500;
    private readonly ConcurrentQueue<ApiLogEntry> _entries = new();

    public void Add(ApiLogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
    }

    public IReadOnlyList<ApiLogEntry> GetAll() => _entries.Reverse().ToList();

    public IReadOnlyList<ApiLogEntry> GetFiltered(string? method, string? path, int? minStatus, int? maxStatus)
    {
        var query = _entries.AsEnumerable().Reverse();

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(e =>
                e.Method.Equals(method, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(path))
            query = query.Where(e =>
                e.Path.Contains(path, StringComparison.OrdinalIgnoreCase));

        if (minStatus.HasValue)
            query = query.Where(e => e.StatusCode >= minStatus.Value);

        if (maxStatus.HasValue)
            query = query.Where(e => e.StatusCode <= maxStatus.Value);

        return query.ToList();
    }

    public void Clear() { while (_entries.TryDequeue(out _)) { } }
}
