using System.Diagnostics.CodeAnalysis;
using LLama;
using MaIN.Domain.Entities;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.LLMService.Memory.Embeddings;
using Microsoft.KernelMemory;

namespace MaIN.Core.IntegrationTests.Fakes;

internal sealed class FakeMemoryFactory : IMemoryFactory
{
    public FakeKernelMemory KernelMemory { get; } = new();

    [Experimental("KMEXP00")]
    public (IKernelMemory km, LLamaSharpTextEmbeddingMaINClone generator, LlamaSharpTextGen textGenerator)
        CreateMemoryWithModel(string modelsPath, LLamaWeights llmModel, string modelName, MemoryParams memoryParams)
        => throw new NotImplementedException("Local model memory not used in this test.");

    public IKernelMemory CreateMemoryWithOpenAi(string openAiKey, MemoryParams memoryParams)
        => KernelMemory;

    public IKernelMemory CreateMemoryWithGemini(string geminiKey, MemoryParams memoryParams)
        => KernelMemory;

    public IKernelMemory CreateMemoryWithVertex(Func<ValueTask<string>> bearerTokenProvider, string location,
        string projectId, MemoryParams memoryParams)
        => KernelMemory;
}
