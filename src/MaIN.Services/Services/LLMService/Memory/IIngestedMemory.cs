using Microsoft.KernelMemory;

namespace MaIN.Services.Services.LLMService.Memory;

internal interface IIngestedMemory : IAsyncDisposable
{
    IKernelMemory Memory { get; }

    Task<string> SearchAsync(string query, CancellationToken cancellationToken = default);
}
