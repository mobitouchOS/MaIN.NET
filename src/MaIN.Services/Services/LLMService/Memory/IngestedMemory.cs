using Microsoft.KernelMemory;

namespace MaIN.Services.Services.LLMService.Memory;

internal sealed class IngestedMemory(
    IKernelMemory memory,
    Func<Task> teardownAsync)
    : IIngestedMemory
{
    public IKernelMemory Memory { get; } = memory;

    public async Task<string> SearchAsync(string query, CancellationToken ct)
    {
        var searchResult = await Memory.SearchAsync(query, cancellationToken: ct);

        var partitions = searchResult.Results
            .SelectMany(r => r.Partitions)
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return partitions.Count > 0
            ? string.Join(Environment.NewLine, partitions)
            : "No relevant content found.";
    }

    public async ValueTask DisposeAsync()
    {
        await teardownAsync();
    }
}
