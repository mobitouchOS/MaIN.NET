using MaIN.Domain.Configuration;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.ImageGenServices;
using Microsoft.Extensions.DependencyInjection;

namespace MaIN.Services.Services.LLMService.Factory;

public class ImageGenServiceFactory(IServiceProvider serviceProvider) : IImageGenServiceFactory
{
    public IImageGenService? CreateService(BackendType backendType)
    {
        return backendType switch
        {
            BackendType.OpenAi => new OpenAiImageGenService(serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<MaINSettings>()),
            BackendType.Gemini => new GeminiImageGenService(serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<MaINSettings>()),
            BackendType.DeepSeek => null,
            BackendType.GroqCloud => null,
            BackendType.Anthropic => null,
            BackendType.Xai => new XaiImageGenService(serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<MaINSettings>()),
            BackendType.Vertex => new VertexImageGenService(serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<MaINSettings>()),
            BackendType.Ollama => null,
            BackendType.Self => new LocalImageGenService(serviceProvider.GetRequiredService<MaINSettings>()),
            _ => throw new NotSupportedException("Not support image generation."),
        };
    }
}