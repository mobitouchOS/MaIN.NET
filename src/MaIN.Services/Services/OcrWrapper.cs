using Microsoft.KernelMemory.DataFormats;
using MaIN.Services.Services.PaddleOcr;

namespace MaIN.Services.Services;

public class OcrWrapper : IOcrEngine
{
    public async Task<string> ExtractTextFromImageAsync(Stream imageContent, CancellationToken cancellationToken = new CancellationToken())
    {
        if (!imageContent.CanRead)
            throw new ArgumentException("Stream is not readable.");

        byte[] imageBytes;
        using (var memoryStream = new MemoryStream())
        {
            await imageContent.CopyToAsync(memoryStream, cancellationToken);
            imageBytes = memoryStream.ToArray();
        }

        return await PaddleOcrProvider.ExtractFromBytesAsync(imageBytes, cancellationToken);
    }

}