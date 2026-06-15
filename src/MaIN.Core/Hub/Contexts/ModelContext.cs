using AsyncKeyedLock;
using MaIN.Core.Hub.Contexts.Interfaces.ModelContext;
using MaIN.Domain.Configuration;
using MaIN.Domain.Exceptions.Models;
using MaIN.Domain.Exceptions.Models.LocalModels;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Constants;
using MaIN.Services.Services.LLMService.Utils;
using System.Diagnostics;
using MaIN.Infrastructure.Models;

namespace MaIN.Core.Hub.Contexts;

public sealed class ModelContext : IModelContext
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _defaultModelsPath;

    private readonly AsyncKeyedLocker<string> _downloadLocks = new();

    private const int DefaultBufferSize = 8192;
    private const int FileStreamBufferSize = 65536;
    private const int ProgressUpdateIntervalMilliseconds = 1000;
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromMinutes(30);
    private const int MaxRetryAttempts = 5;
    private static readonly TimeSpan ReadStallTimeout = TimeSpan.FromSeconds(30);

    internal ModelContext(MaINSettings settings, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _defaultModelsPath = Environment.GetEnvironmentVariable("MaIN_ModelsPath") ?? settings.ModelsPath;
    }

    public IEnumerable<AIModel> GetAll() => ModelRegistry.GetAll();

    public IEnumerable<LocalModel> GetAllLocal() => ModelRegistry.GetAllLocal();

    public AIModel GetModel(string modelId) => ModelRegistry.GetById(modelId);

    public AIModel GetEmbeddingModel() => new Mxbai_Embedding();

    public bool Exists(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new MissingModelIdException(nameof(modelId));
        }

        var model = ModelRegistry.GetById(modelId);
        if (model is not LocalModel localModel)
        {
            return false; // Cloud models don't have local files
        }

        return localModel.IsDownloaded(_defaultModelsPath);
    }

    public Task<IModelContext> EnsureDownloadedAsync(string modelId, CancellationToken cancellationToken = default)
        => EnsureDownloadedAsync(modelId, null, cancellationToken);

    public async Task<IModelContext> EnsureDownloadedAsync(string modelId, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new MissingModelIdException(nameof(modelId));
        }

        var model = ModelRegistry.GetById(modelId);

        if (model is not LocalModel localModel)
        {
            throw new InvalidModelTypeException(nameof(LocalModel));
        }

        if (localModel.IsDownloaded(_defaultModelsPath))
        {
            return this;
        }

        using (await _downloadLocks.LockAsync(modelId, cancellationToken))
        {
            // Double-check
            if (!localModel.IsDownloaded(_defaultModelsPath))
            {
                await DownloadModelAsync(localModel, progress, cancellationToken);
            }
        }

        return this;
    }

    public async Task<IModelContext> EnsureDownloadedAsync<TModel>(CancellationToken cancellationToken = default) where TModel : LocalModel, new()
    {
        var model = new TModel();
        return await EnsureDownloadedAsync(model.Id, cancellationToken);
    }

    public Task<IModelContext> DownloadAsync(string modelId, CancellationToken cancellationToken = default)
        => DownloadAsync(modelId, null, cancellationToken);

    public async Task<IModelContext> DownloadAsync(string modelId, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new MissingModelIdException(nameof(modelId));
        }

        var model = ModelRegistry.GetById(modelId);

        if (model is not LocalModel localModel)
        {
            throw new InvalidModelTypeException(nameof(LocalModel));
        }

        using (await _downloadLocks.LockAsync(modelId, cancellationToken))
        {
            await DownloadModelAsync(localModel, progress, cancellationToken);
        }

        return this;
    }

    public IModelContext LoadToCache(LocalModel model)
    {
        var path = model.CustomPath ?? _defaultModelsPath;
        if (string.IsNullOrEmpty(path))
        {
            throw new ModelPathNullOrEmptyException();
        }

        ModelLoader.GetOrLoadModel(path, model.FileName);
        return this;
    }

    public async Task<IModelContext> LoadToCacheAsync(LocalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        await ModelLoader.GetOrLoadModelAsync(GetModelFilePath(model), model.FileName);
        return this;
    }

    private async Task DownloadModelAsync(LocalModel localModel, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        foreach (var asset in localModel.RequiredAssets)
        {
            var assetPath = localModel.GetAssetPath(asset, _defaultModelsPath);
            if (File.Exists(assetPath))
            {
                continue;
            }

            if (asset.DownloadUrl is null) throw new DownloadUrlNullOrEmptyException();

            await DownloadAssetAsync(asset.DownloadUrl, assetPath, asset.FileName, progress, cancellationToken);
        }
    }

    private async Task DownloadAssetAsync(Uri downloadUrl, string filePath, string fileName, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Starting download of {fileName}...");

        for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(2 * Math.Pow(2, attempt - 1), 60));
                Console.WriteLine($"\nRetrying ({attempt}/{MaxRetryAttempts}) in {delay.TotalSeconds:F0}s...");
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                await TryDownloadWithResumeAsync(downloadUrl, filePath, fileName, progress, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts)
            {
                Console.WriteLine($"\nDownload error: {ex.Message}");
            }
        }

        if (File.Exists(filePath)) File.Delete(filePath);
        throw new IOException($"Download of {fileName} failed after {MaxRetryAttempts} retries.");
    }

    private async Task TryDownloadWithResumeAsync(Uri url, string filePath, string fileName, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var resumeFrom = File.Exists(filePath) ? new FileInfo(filePath).Length : 0L;

        using var httpClient = CreateConfiguredHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumeFrom > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            Console.WriteLine("\nFile already fully downloaded.");
            return;
        }

        var isResume = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        if (!isResume) resumeFrom = 0;

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength is { } cl
            ? cl + (isResume ? resumeFrom : 0)
            : null;

        if (isResume)
            Console.WriteLine($"Resuming from {FormatBytes(resumeFrom)}...");
        else if (totalBytes.HasValue)
            Console.WriteLine($"File size: {FormatBytes(totalBytes.Value)}");

        var fileMode = isResume ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None, FileStreamBufferSize);
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await ReadChunksAsync(contentStream, fileStream, resumeFrom, totalBytes, fileName, progress, cancellationToken);
    }

    private static async Task ReadChunksAsync(Stream content, FileStream file, long startOffset, long? totalBytes, string fileName, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var totalBytesRead = startOffset;
        var buffer = new byte[DefaultBufferSize];
        var progressStopwatch = Stopwatch.StartNew();
        var totalStopwatch = Stopwatch.StartNew();

        while (true)
        {
            int bytesRead;
            using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                stallCts.CancelAfter(ReadStallTimeout);
                try
                {
                    bytesRead = await content.ReadAsync(buffer, stallCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new IOException($"Download stalled: no data received for {ReadStallTimeout.TotalSeconds:F0}s.");
                }
            }

            if (bytesRead == 0) break;

            await file.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            if (ShouldUpdateProgress(progressStopwatch))
            {
                var elapsed = totalStopwatch.Elapsed.TotalSeconds;
                var speed = elapsed > 0 ? totalBytesRead / elapsed : 0;
                ShowProgress(totalBytesRead, totalBytes, totalStopwatch);
                progress?.Report(new DownloadProgress(totalBytesRead, totalBytes, speed));
                progressStopwatch.Restart();
            }
        }

        var totalTime = totalStopwatch.Elapsed;
        var avgSpeed = totalTime.TotalSeconds > 0 ? totalBytesRead / totalTime.TotalSeconds : 0;
        Console.WriteLine($"\nDownload completed: {fileName}. " +
                          $"Total: {FormatBytes(totalBytesRead)}, Time: {totalTime:hh\\:mm\\:ss}, Speed: {FormatBytes((long)avgSpeed)}/s");
        progress?.Report(new DownloadProgress(totalBytesRead, totalBytes, 0));
    }

    private HttpClient CreateConfiguredHttpClient()
    {
        var httpClient = _httpClientFactory.CreateClient(ServiceConstants.HttpClients.ModelContextDownloadClient);
        httpClient.Timeout = DefaultHttpTimeout;
        return httpClient;
    }

    private string GetModelFilePath(LocalModel localModel) =>
        localModel.GetFullPath(_defaultModelsPath);

    private static bool ShouldUpdateProgress(Stopwatch progressStopwatch) =>
        progressStopwatch.ElapsedMilliseconds >= ProgressUpdateIntervalMilliseconds;

    private static void ShowProgress(long totalBytesRead, long? totalBytes, Stopwatch totalStopwatch)
    {
        var elapsedSeconds = totalStopwatch.Elapsed.TotalSeconds;
        var speed = elapsedSeconds > 0 ? totalBytesRead / elapsedSeconds : 0;

        if (totalBytes.HasValue)
        {
            var progressPercentage = (double)totalBytesRead / totalBytes.Value * 100;
            var eta = speed > 0 ? TimeSpan.FromSeconds((totalBytes.Value - totalBytesRead) / speed) : TimeSpan.Zero;

            var (leftBefore, topBefore) = Console.GetCursorPosition();
            Console.Write($"\rProgress: {progressPercentage:F1}% ({FormatBytes(totalBytesRead)}/{FormatBytes(totalBytes.Value)}) " +
                         $"Speed: {FormatBytes((long)speed)}/s ETA: {eta:hh\\:mm\\:ss}");

            var (leftAfter, topAfter) = Console.GetCursorPosition();
            int lengthDifference = leftBefore - leftAfter + ((topBefore - topAfter) * Console.WindowWidth);
            while (lengthDifference > 0)
            {
                Console.Write(' ');
                lengthDifference--;
            }
            Console.SetCursorPosition(leftAfter, topAfter);
        }
        else
        {
            Console.Write($"\rDownloaded: {FormatBytes(totalBytesRead)} Speed: {FormatBytes((long)speed)}/s");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0)
        {
            return "0 Bytes";
        }

        const int scale = 1024;
        string[] orders = ["GB", "MB", "KB", "Bytes"];
        var max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (var order in orders)
        {
            if (bytes >= max)
            {
                return $"{decimal.Divide(bytes, max):##.##} {order}";
            }

            max /= scale;
        }

        return "0 Bytes";
    }
}
