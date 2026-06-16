using Examples.Utils;
using MaIN.Core.Hub;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;

namespace Examples.Chat;

public class ChatWithImageGenExample : IExample
{
    public async Task Start()
    {
        Console.WriteLine("ChatExample with image gen is running!");

        ModelRegistry.RegisterOrReplace(new StableDiffusion1_5());

        await AIHub.Model().EnsureDownloadedAsync(Models.Local.StableDiffusion1_5);

        var result = await AIHub.Chat()
            .WithModel(Models.Local.StableDiffusion1_5)
            .WithMessage("Fluffy cat with a book - anime style")
            .CompleteAsync();

        ImagePreview.ShowImage(result.Message.Image);
    }
}
