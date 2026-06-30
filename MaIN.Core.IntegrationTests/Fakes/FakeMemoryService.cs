using MaIN.Services.Services.LLMService;
using MaIN.Services.Services.LLMService.Memory;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;

namespace MaIN.Core.IntegrationTests.Fakes;

internal sealed class FakeMemoryService : IMemoryService
{
    public Task ImportDataToMemory((IKernelMemory km, ITextEmbeddingGenerator? generator) memory,
        ChatMemoryOptions options, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public string CleanResponseText(string text) => text;
}
