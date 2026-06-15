using HPPH.SkiaSharp;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Exceptions.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.ImageGenServices.Utils;
using MaIN.Services.Services.Models;
using StableDiffusion.NET;

namespace MaIN.Services.Services.ImageGenServices;

public class LocalImageGenService(MaINSettings settings) : IImageGenService
{
    private readonly MaINSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public Task<ChatResult?> Send(Chat chat)
    {
        var model = ModelRegistry.GetById(chat.ModelId);

        if (model is not LocalModel localModel || model is not ILocalDiffusionModel capabilities)
        {
            throw new InvalidModelTypeException(nameof(ILocalDiffusionModel));
        }

        var diffusionModel = DiffusionModelLoader.GetOrLoad(localModel, capabilities, _settings.ModelsPath);

        var prompt = BuildPrompt(chat.Messages);
        var parameters = ImageGenerationParameter
            .TextToImage(prompt)
            .WithSize(capabilities.Width, capabilities.Height)
            .WithCfg(capabilities.CfgScale)
            .WithSteps(capabilities.Steps);

        var image = diffusionModel.GenerateImage(parameters)
                     ?? throw new InvalidOperationException("Image generation failed.");

        return Task.FromResult<ChatResult?>(CreateChatResult(image.ToPng(), chat.ModelId));
    }

    private static string BuildPrompt(ICollection<Message> messages)
    {
        return messages
            .Select((msg, index) => index == 0 ? msg.Content : $"&& {msg.Content}")
            .Aggregate((current, next) => $"{current} {next}");
    }

    private static ChatResult CreateChatResult(byte[] imageBytes, string modelId)
    {
        return new ChatResult
        {
            Done = true,
            Message = new Message
            {
                Content = ServiceConstants.Messages.GeneratedImageContent,
                Role = ServiceConstants.Roles.Assistant,
                Image = imageBytes,
                Type = MessageType.Image
            },
            Model = modelId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
