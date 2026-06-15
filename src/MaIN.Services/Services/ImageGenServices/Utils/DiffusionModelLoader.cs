using System.Collections.Concurrent;
using MaIN.Domain.Models.Abstract;
using StableDiffusion.NET;

namespace MaIN.Services.Services.ImageGenServices.Utils;

public static class DiffusionModelLoader
{
    private static readonly ConcurrentDictionary<string, DiffusionModel> ModelCache = new();

    public static DiffusionModel GetOrLoad(LocalModel model, ILocalDiffusionModel capabilities, string? basePath)
    {
        var key = model.GetFullPath(basePath);

        return ModelCache.GetOrAdd(key, _ =>
        {
            var parameters = DiffusionModelParameter.Create()
                .WithModelPath(model.GetFullPath(basePath))
                .WithMultithreading()
                .WithFlashAttention();

            if (capabilities.Vae is { } vae)
            {
                parameters = parameters.WithVae(model.GetAssetPath(vae, basePath));
            }

            if (capabilities.ClipL is { } clipL)
            {
                parameters = parameters.WithClipLPath(model.GetAssetPath(clipL, basePath));
            }

            if (capabilities.ClipG is { } clipG)
            {
                parameters = parameters.WithClipGPath(model.GetAssetPath(clipG, basePath));
            }

            if (capabilities.T5Xxl is { } t5Xxl)
            {
                parameters = parameters.WithT5xxlPath(model.GetAssetPath(t5Xxl, basePath));
            }

            if (capabilities.Qwen2VL is { } qwen2VL)
            {
                parameters = parameters.WithLLMVisionPath(model.GetAssetPath(qwen2VL, basePath));
            }

            return new DiffusionModel(parameters);
        });
    }

    public static void RemoveModel(string fullPath)
    {
        if (ModelCache.TryRemove(fullPath, out var model))
        {
            model.Dispose();
        }
    }
}
