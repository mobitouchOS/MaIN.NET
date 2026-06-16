using System.Collections.Concurrent;
using MaIN.Domain.Models.Abstract;
using StableDiffusion.NET;

namespace MaIN.Services.Services.ImageGenServices.Utils;

public static class DiffusionModelLoader
{
    private const int MaxRecentLogLines = 50;

    private static readonly ConcurrentDictionary<string, DiffusionModel> ModelCache = new();
    private static readonly ConcurrentQueue<string> RecentLogLines = new();
    private static readonly object EventsInitLock = new();
    private static bool _eventsInitialized;

    public static DiffusionModel GetOrLoad(LocalModel model, ILocalDiffusionModel capabilities, string? basePath)
    {
        var key = model.GetFullPath(basePath);

        return ModelCache.GetOrAdd(key, _ =>
        {
            EnsureLogCaptureInitialized();

            var parameters = DiffusionModelParameter.Create()
                .WithMultithreading()
                .WithFlashAttention();

            // SD1/SDXL GGUFs are full checkpoints (unet+vae+clip in one file); FLUX/SD3/Qwen-Image
            // GGUFs only contain the diffusion transformer and need separate vae/clip/t5xxl/etc.
            parameters = capabilities.Architecture switch
            {
                DiffusionArchitecture.SD1 or DiffusionArchitecture.SDXL
                    => parameters.WithModelPath(model.GetFullPath(basePath)),
                _ => parameters.WithDiffusionModelPath(model.GetFullPath(basePath))
            };

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

            try
            {
                return new DiffusionModel(parameters);
            }
            catch (Exception ex)
            {
                var log = string.Join(Environment.NewLine, RecentLogLines);
                var details = string.IsNullOrWhiteSpace(log)
                    ? string.Empty
                    : $"{Environment.NewLine}Native log:{Environment.NewLine}{log}";

                throw new InvalidOperationException(
                    $"Failed to load diffusion model '{key}'. {ex.Message}{details}", ex);
            }
        });
    }

    public static void RemoveModel(string fullPath)
    {
        if (ModelCache.TryRemove(fullPath, out var model))
        {
            model.Dispose();
        }
    }

    /// <summary>
    /// Wires up stable-diffusion.cpp's native log callback so errors/warnings raised while
    /// loading or initializing a model (eg. out-of-memory, missing tensors) are captured and
    /// can be surfaced in thrown exceptions instead of the generic native error.
    /// </summary>
    private static void EnsureLogCaptureInitialized()
    {
        if (_eventsInitialized)
        {
            return;
        }

        lock (EventsInitLock)
        {
            if (_eventsInitialized)
            {
                return;
            }

            StableDiffusionCpp.InitializeEvents();
            StableDiffusionCpp.Log += (sender, e) =>
            {
                RecentLogLines.Enqueue($"[{e.Level}] {e.Text}");
                while (RecentLogLines.Count > MaxRecentLogLines && RecentLogLines.TryDequeue(out _))
                {
                }
            };

            _eventsInitialized = true;
        }
    }
}
