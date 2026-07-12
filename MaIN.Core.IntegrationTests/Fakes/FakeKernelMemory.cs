using System.Runtime.CompilerServices;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;

namespace MaIN.Core.IntegrationTests.Fakes;

internal sealed class FakeKernelMemory : IKernelMemory
{
    public int SearchCallCount { get; private set; }
    public List<string> SearchQueries { get; } = [];

    public Task<SearchResult> SearchAsync(
        string query,
        string? index = null,
        MemoryFilter? filter = null,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        int limit = -1,
        IContext? context = null,
        CancellationToken ct = default)
    {
        SearchCallCount++;
        SearchQueries.Add(query);
        return Task.FromResult(new SearchResult
        {
            Results =
            [
                new Citation
                {
                    SourceName = "test-doc",
                    Partitions = [new Citation.Partition
                        { Text = "Copernicus proposed the heliocentric model of the solar system." }
                    ]
                }
            ]
        });
    }

    public Task DeleteIndexAsync(
        string? index = null,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public async IAsyncEnumerable<MemoryAnswer> AskStreamingAsync(
        string question,
        string? index = null,
        MemoryFilter? filter = null,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        SearchOptions? options = null,
        IContext? context = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new MemoryAnswer { Question = question, Result = "Fake answer.", NoResult = false };
        await Task.CompletedTask;
    }

    public Task<string> ImportDocumentAsync(
        Stream content,
        string? fileName = null,
        string? documentId = null,
        TagCollection? tags = null,
        string? index = null,
        IEnumerable<string>? steps = null,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ImportDocumentAsync(
        string filePath,
        string? documentId = null,
        TagCollection? tags = null,
        string? index = null,
        IEnumerable<string>? steps = null,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ImportDocumentAsync(
        DocumentUploadRequest uploadRequest,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ImportDocumentAsync(
        Document document,
        string? index = null,
        IEnumerable<string>? steps = null,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ImportTextAsync(
        string text,
        string? documentId = null,
        TagCollection? tags = null,
        string? index = null,
        IEnumerable<string>? steps = null,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ImportWebPageAsync(
        string url,
        string? documentId = null,
        TagCollection? tags = null,
        string? index = null,
        IEnumerable<string>? steps = null,
        IContext? context = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<StreamableFileContent> ExportFileAsync(
        string documentId,
        string fileName,
        string? index = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<bool> IsDocumentReadyAsync(
        string documentId,
        string? index = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<DataPipelineStatus?> GetDocumentStatusAsync(
        string documentId,
        string? index = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteDocumentAsync(
        string documentId,
        string? index = null,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<IndexDetails>> ListIndexesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException();

}
