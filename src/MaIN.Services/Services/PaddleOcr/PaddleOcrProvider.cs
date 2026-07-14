using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;

namespace MaIN.Services.Services.PaddleOcr;

internal static class PaddleOcrProvider
{
    private static readonly object EngineLock = new();
    private static QueuedPaddleOcrAll? _engine;

    private static QueuedPaddleOcrAll GetEngine()
    {
        if (_engine is not null) return _engine;

        lock (EngineLock)
        {
            if (_engine is not null) return _engine;

            FullOcrModel model = OnlineFullModels.EnglishV4.DownloadAsync().GetAwaiter().GetResult();
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
    }

    public static async Task<string> ExtractFromBytesAsync(byte[] bytes, CancellationToken ct = default)
    {
        using var src = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (src.Empty())
        {
            throw new ArgumentException("Failed to decode image from the provided byte array.", nameof(bytes));
        }

        var result = await GetEngine().Run(src, cancellationToken: ct);
        return result.Text;
    }

    public static async Task<string> ExtractFromFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The specified image file was not found.", path);
        }

        using var src = Cv2.ImRead(path, ImreadModes.Color);
        if (src.Empty())
        {
            throw new ArgumentException($"Failed to load image from file: {path}");
        }

        var result = await GetEngine().Run(src, cancellationToken: ct);
        return result.Text;
    }
}
