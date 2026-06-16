using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Skills;
using MaIN.Services.Constants;
using MaIN.Services.Services;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.ImageGenServices;
using MaIN.Services.Services.LLMService;
using MaIN.Services.Services.LLMService.Factory;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.Models.Commands;
using MaIN.Services.Services.Skills;
using MaIN.Services.Services.Steps;
using MaIN.Services.Services.Steps.Commands;
using MaIN.Services.Services.Steps.Commands.Abstract;
using MaIN.Services.Services.TTSService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaIN.Services;

public static class Bootstrapper
{
    public static IServiceCollection ConfigureMaIN(
        this IServiceCollection serviceCollection,
        IConfiguration configuration,
        Action<MaINSettings>? configureSettings = null)
    {
        var settings = configuration.GetSection(MainSectionName).Get<MaINSettings>() ?? new MaINSettings();
        
        configureSettings?.Invoke(settings);
        serviceCollection.AddSingleton(settings);
        
        serviceCollection.AddSingleton<IChatService, ChatService>();
        serviceCollection.AddSingleton<IAgentService, AgentService>();
        serviceCollection.AddSingleton<INotificationService, NotificationService>();
        serviceCollection.AddSingleton<IAgentFlowService, AgentFlowService>();
        serviceCollection.AddSingleton<ITranslatorService, TranslatorService>();
        serviceCollection.AddSingleton<IMemoryService, MemoryService>();
        serviceCollection.AddSingleton<IMemoryFactory, MemoryFactory>();
        serviceCollection.AddSingleton<ILLMServiceFactory, LLMServiceFactory>();
        serviceCollection.AddSingleton<IImageGenServiceFactory, ImageGenServiceFactory>();
        serviceCollection.AddSingleton<ITTSServiceFactory, TTSServiceFactory>();

// Register all concrete implementations as transient
        serviceCollection.AddTransient<LLMService>();
        serviceCollection.AddTransient<OpenAiService>();
        serviceCollection.AddTransient<LocalImageGenService>();
        serviceCollection.AddTransient<OpenAiImageGenService>();
        
        serviceCollection.AddTransient<ITextToSpeechService, TextToSpeechService>();
        
        // Register all step handlers
        serviceCollection.AddSingleton<IStepHandler, RedirectStepHandler>();
        serviceCollection.AddSingleton<IStepHandler, FetchDataStepHandler>();
        serviceCollection.AddSingleton<IStepHandler, AnswerStepHandler>();
        serviceCollection.AddSingleton<IStepHandler, McpStepHandler>();
        serviceCollection.AddSingleton<IStepHandler, BecomeStepHandler>();
        serviceCollection.AddSingleton<IStepHandler, CleanupStepHandler>();
        serviceCollection.AddCommandHandlers();
        
        //AddHttpClients
        serviceCollection.AddHttpClients();
        
        // Register the step processor
        serviceCollection.AddSingleton<IStepProcessor, StepProcessor>();

        // Register skill infrastructure
        serviceCollection.AddSingleton<ISkillRegistry>(sp => new SkillRegistry(
            sp.GetServices<IAgentSkillProvider>(),
            sp.GetServices<ISkillLoader>(),
            sp.GetRequiredService<ILogger<SkillRegistry>>()));
        serviceCollection.AddSingleton<ISkillComposer, SkillComposer>();

        // Provider-side Skills API infrastructure (OpenAI Responses, Anthropic container.skills).
        serviceCollection.AddSingleton<IProviderSkillCache, ProviderSkillCache>();
        serviceCollection.AddSingleton<IProviderSkillUploader, OpenAiSkillUploader>();
        serviceCollection.AddSingleton<IProviderSkillUploader, AnthropicSkillUploader>();
        serviceCollection.AddSingleton<ProviderSkillUploadCoordinator>();

        // Register skills directory loader if configured
        if (!string.IsNullOrWhiteSpace(settings.SkillsDirectory))
        {
            serviceCollection.AddSingleton<ISkillLoader>(sp =>
                new FileSystemSkillLoader(
                    settings.SkillsDirectory,
                    sp.GetRequiredService<ILogger<FileSystemSkillLoader>>()));
        }

        return serviceCollection;
    }

    /// <summary>
    /// Uploads every uploadable SKILL.md bundle to cloud providers with credentials configured.
    /// Call once after the host is built and before any agent creation. Safe to call when no
    /// cloud keys are set — it just no-ops.
    /// </summary>
    public static async Task UploadSkillsToProvidersAsync(this IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        var coordinator = provider.GetService<ProviderSkillUploadCoordinator>();
        if (coordinator is null) return;
        await coordinator.RunAsync(cancellationToken);
    }

    public static IServiceCollection AddSkillsFromDirectory(
        this IServiceCollection serviceCollection, string directoryPath)
    {
        serviceCollection.AddSingleton<ISkillLoader>(sp =>
            new FileSystemSkillLoader(
                directoryPath,
                sp.GetRequiredService<ILogger<FileSystemSkillLoader>>()));
        return serviceCollection;
    }

    private static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IDataSourceProvider, DataSourceProvider>();
        
        services.AddSingleton<ICommandHandler<StartCommand, Message?>, StartCommandHandler>();
        services.AddSingleton<ICommandHandler<RedirectCommand, Message?>, RedirectCommandHandler>();
        services.AddSingleton<ICommandHandler<FetchCommand, Message?>, FetchCommandHandler>();
        services.AddSingleton<ICommandHandler<AnswerCommand, Message?>, AnswerCommandHandler>();
        services.AddSingleton<ICommandHandler<McpCommand, Message?>, McpCommandHandler>();

        services.AddSingleton<ICommandDispatcher, CommandDispatcher>(provider =>
        {
            var dispatcher = new CommandDispatcher(provider);
            
            dispatcher.RegisterNamedHandler<McpCommand, Message?, McpCommandHandler>("MCP");
            dispatcher.RegisterNamedHandler<StartCommand, Message?, StartCommandHandler>("START");
            dispatcher.RegisterNamedHandler<RedirectCommand, Message?, RedirectCommandHandler>("REDIRECT");
            dispatcher.RegisterNamedHandler<FetchCommand, Message?, FetchCommandHandler>("FETCH_DATA");
            dispatcher.RegisterNamedHandler<FetchCommand, Message?, FetchCommandHandler>("FETCH_DATA*");
            dispatcher.RegisterNamedHandler<AnswerCommand, Message?, AnswerCommandHandler>("ANSWER");
            
            return dispatcher;
        });
        
        services.AddSingleton<StartCommandHandler>();
        services.AddSingleton<RedirectCommandHandler>();
        services.AddSingleton<FetchCommandHandler>();
        services.AddSingleton<AnswerCommandHandler>();
        services.AddSingleton<McpCommandHandler>();

        return services;
    }
    
    private static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(ServiceConstants.HttpClients.OpenAiClient);
        services.AddHttpClient(ServiceConstants.HttpClients.GeminiClient);
        services.AddHttpClient(ServiceConstants.HttpClients.DeepSeekClient);
        services.AddHttpClient(ServiceConstants.HttpClients.GroqCloudClient);
        services.AddHttpClient(ServiceConstants.HttpClients.OllamaClient);
        services.AddHttpClient(ServiceConstants.HttpClients.OllamaLocalClient);
        services.AddHttpClient(ServiceConstants.HttpClients.VertexClient);
        services.AddHttpClient(ServiceConstants.HttpClients.ImageDownloadClient);
        services.AddHttpClient(ServiceConstants.HttpClients.ModelContextDownloadClient, client =>
        {
            client.Timeout = TimeSpan.FromHours(10);
        });
        return services;
    }

    private const string MainSectionName = "MaIN";
}
