using System.Diagnostics.CodeAnalysis;
using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.KernelMemory;
using MaIN.Domain.Entities;
using MaIN.Domain.Exceptions.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Services.LLMService.Memory.Embeddings;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using InferenceParams = LLama.Common.InferenceParams;
#pragma warning disable CS0618 // Type or member is obsolete

namespace MaIN.Services.Services.LLMService.Memory;

public class MemoryFactory() : IMemoryFactory
{
    [Experimental("KMEXP00")]
    public (IKernelMemory km, LLamaSharpTextEmbeddingMaINClone generator, LlamaSharpTextGen textGenerator)
        CreateMemoryWithModel(string modelsPath,
            LLamaWeights model,
            string modelName,
            MemoryParams memoryParams)
    {
        var path = ResolvePath(modelsPath);
        var embeddingModel = (LocalModel)ModelRegistry.GetById(MaIN.Domain.Models.Models.Local.MxbaiEmbedding);
        var embeddingModelPath = Path.Combine(path, embeddingModel.FileName);
        var modelPath = Path.Combine(path, modelName);
        var generator = ConfigureGeneratorOptions(embeddingModelPath, modelPath, memoryParams);
        var searchOptions = ConfigureSearchOptions(memoryParams);
        var parsingOptions = ConfigureParsingOptions();
        var modelParams = new ModelParams(modelPath)
        {
            ContextSize = (uint)memoryParams.ContextSize,
            GpuLayerCount = memoryParams.GpuLayerCount,
        };
        
        var km = new KernelMemoryBuilder()
            .WithLLamaSharpTextGeneration(model, modelParams, memoryParams, out var textGen)
            .WithLLamaSharpTextEmbeddingOwnGeneration(generator)
            .WithSearchClientConfig(searchOptions)
            .WithCustomImageOcr(new OcrWrapper())
            .With(parsingOptions)
            .Build<MemoryServerless>();
        
        return (km, generator, textGen);
    }

    public IKernelMemory CreateMemoryWithOpenAi(string openAiKey, MemoryParams memoryParams)
    {
        var searchOptions = ConfigureSearchOptions(memoryParams);

        var kernelMemory = new KernelMemoryBuilder()
            .WithSearchClientConfig(searchOptions)
            .WithOpenAIDefaults(openAiKey)
            .Build();

        return kernelMemory;
    }

    public IKernelMemory CreateMemoryWithGemini(string geminiKey, MemoryParams memoryParams)
    {
        var searchOptions = ConfigureSearchOptions(memoryParams);

        var kernelMemory = new KernelMemoryBuilder()
            .WithSearchClientConfig(searchOptions)
#pragma warning disable SKEXP0070 // For evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            .WithSemanticKernelTextGenerationService(
                new GeminiTextGeneratorAdapter(new GoogleAIGeminiChatCompletionService("gemini-2.5-flash", geminiKey)),
                new SemanticKernelConfig())
            .WithSemanticKernelTextEmbeddingGenerationService(
                new GoogleAITextEmbeddingGenerationService("gemini-embedding-001", geminiKey), new SemanticKernelConfig())
#pragma warning restore SKEXP0070
            .WithCustomImageOcr(new OcrWrapper())
            .WithSimpleVectorDb()
            .Build();

        return kernelMemory;
    }

    public IKernelMemory CreateMemoryWithVertex(Func<ValueTask<string>> bearerTokenProvider, string location, string projectId, MemoryParams memoryParams)
    {
        var searchOptions = ConfigureSearchOptions(memoryParams);

        var kernelMemory = new KernelMemoryBuilder()
            .WithSearchClientConfig(searchOptions)
#pragma warning disable SKEXP0070
            .WithSemanticKernelTextGenerationService(
                new GeminiTextGeneratorAdapter(
                    new VertexAIGeminiChatCompletionService("gemini-2.5-flash", bearerTokenProvider, location, projectId)),
                new SemanticKernelConfig())
            .WithSemanticKernelTextEmbeddingGenerationService(
                new VertexAITextEmbeddingGenerationService("text-embedding-005", bearerTokenProvider, location, projectId),
                new SemanticKernelConfig())
#pragma warning restore SKEXP0070
            .WithCustomImageOcr(new OcrWrapper())
            .WithSimpleVectorDb()
            .Build();

        return kernelMemory;
    }

    #region Private Configuration Methods

    private string ResolvePath(string modelsPath)
    {
        var path = modelsPath;

        return string.IsNullOrEmpty(path) 
            ? throw new ModelsPathNotFoundException() 
            : path;
    }

    private static LLamaSharpTextEmbeddingMaINClone ConfigureGeneratorOptions(string embeddingModelPath,
        string modelPath, MemoryParams memoryParams)
    {
        var inferenceParams = new InferenceParams
        {
            AntiPrompts = ["INFO", "<|im_end|>", "Question:"]
        };

        var desiredPath = memoryParams.MultiModalMode ? modelPath : embeddingModelPath;
        var config = new LLamaSharpConfig(desiredPath)
        {
            DefaultInferenceParams = inferenceParams,
            GpuLayerCount = memoryParams.GpuLayerCount,
            ContextSize = (uint?)memoryParams.ContextSize,
        };

        var parameters = new ModelParams(config.ModelPath)
        {
            ContextSize = 0, // let the model decide (mxbai-embed-large-v1 = 512)
            GpuLayerCount = config.GpuLayerCount.GetValueOrDefault(20),
            Embeddings = true,
            UseMemorymap = true,
            PoolingType = LLamaPoolingType.CLS,
        };

        var weights = LLamaWeights.LoadFromFile(parameters);

        // Override config context size for embedding — use model's native context
        config = new LLamaSharpConfig(desiredPath)
        {
            DefaultInferenceParams = inferenceParams,
            GpuLayerCount = memoryParams.GpuLayerCount,
            ContextSize = 0,
        };
        return new LLamaSharpTextEmbeddingMaINClone(config, weights);
    }

    private static SearchClientConfig ConfigureSearchOptions(MemoryParams memoryParams)
    {
        return new SearchClientConfig
        {
            MaxMatchesCount = memoryParams.MaxMatchesCount,
            FrequencyPenalty = memoryParams.FrequencyPenalty,
            Temperature = memoryParams.Temperature,
            AnswerTokens = memoryParams.AnswerTokens,
        };
    }

    private static TextPartitioningOptions ConfigureParsingOptions()
    {
        return new TextPartitioningOptions
        {
            MaxTokensPerParagraph = 400,
            OverlappingTokens = 20,
        };
    }

    #endregion
}