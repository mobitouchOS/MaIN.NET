using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;

namespace MaIN.Services.Services.PaddleOcr;

/// <summary>
/// Shared, process-wide OCR engine backed by PaddleOCR (cross-platform replacement for Tesseract).
/// A raw <see cref="PaddleOcrAll"/> is not thread-safe and is expensive to initialize, so all
/// callers share one <see cref="QueuedPaddleOcrAll"/> instance, which pools several workers behind
/// a bounded queue and is safe to call concurrently from many requests.
/// </summary>
internal static class PaddleOcrProvider
{
    private static readonly Lazy<QueuedPaddleOcrAll> Engine = new(() =>
    {
        FullOcrModel model = OnlineFullModels.EnglishV4.DownloadAsync().GetAwaiter().GetResult();
        var workers = Math.Max(1, Environment.ProcessorCount / 2);
        return new QueuedPaddleOcrAll(
            () => new PaddleOcrAll(model, PaddleDevice.Blas())
            {
                // Rotation detection warps upright text boxes and degrades recognition
                // on stylized/logo-style text (verified: garbles "CALL OF DUTY" into "CAUTUTY").
                AllowRotateDetection = false,
                Enable180Classification = false,
            },
            consumerCount: workers,
            boundedCapacity: 64);
    });

    public static async Task<string> ExtractFromBytesAsync(byte[] bytes, CancellationToken ct = default)
    {
        using var src = Cv2.ImDecode(bytes, ImreadModes.Color);
        var result = await Engine.Value.Run(src, cancellationToken: ct);
        return result.Text;
    }

    public static async Task<string> ExtractFromFileAsync(string path, CancellationToken ct = default)
    {
        using var src = Cv2.ImRead(path, ImreadModes.Color);
        var result = await Engine.Value.Run(src, cancellationToken: ct);
        return result.Text;
    }
}
