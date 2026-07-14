using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;

namespace MaIN.Services.Services.PaddleOcr;

internal static class PaddleOcrProvider
{
    private static readonly SemaphoreSlim EngineLock = new(1, 1);
    private static QueuedPaddleOcrAll? _engine;

    private static async Task<QueuedPaddleOcrAll> GetEngineAsync(CancellationToken ct)
    {
        if (_engine is not null) return _engine;

        await EngineLock.WaitAsync(ct);
        try
        {
            if (_engine is not null) return _engine;

            FullOcrModel model = await OnlineFullModels.EnglishV4.DownloadAsync();
            var workers = Math.Max(1, Environment.ProcessorCount / 2);
            _engine = new QueuedPaddleOcrAll(
                () => new PaddleOcrAll(model, PaddleDevice.Blas())
                {
                    // Rotation detection warps upright text boxes and degrades recognition
                    // on stylized/logo-style text (verified: garbles "CALL OF DUTY" into "CAUTUTY").
                    AllowRotateDetection = false,
                    Enable180Classification = false,
                },
                consumerCount: workers,
                boundedCapacity: 64);
            return _engine;
        }
        finally
        {
            EngineLock.Release();
        }
    }

    public static async Task<string> ExtractFromBytesAsync(byte[] bytes, CancellationToken ct = default)
    {
        using var src = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (src.Empty())
        {
            throw new ArgumentException("Failed to decode image from the provided byte array.", nameof(bytes));
        }

        var engine = await GetEngineAsync(ct);
        var result = await engine.Run(src, cancellationToken: ct);
        return result.Text;
    }

    public static async Task<string> ExtractFromFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The specified image file was not found.", path);
        }
        
        byte[] fileBytes = await File.ReadAllBytesAsync(path, ct);
        return await ExtractFromBytesAsync(fileBytes, ct);
    }
}
