using Microsoft.KernelMemory;

namespace MaIN.Services.Services.LLMService.Memory;

public interface IIngestedMemory : IAsyncDisposable
{
    IKernelMemory Memory { get; }

    Task<string> SearchAsync(string query, CancellationToken cancellationToken = default);
}
