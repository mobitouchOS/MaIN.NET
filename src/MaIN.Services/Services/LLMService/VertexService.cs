using System.Text;
using MaIN.Domain.Configuration;
using MaIN.Domain.Configuration.BackendInferenceParams;
using MaIN.Domain.Configuration.Vertex;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Concrete;
using MaIN.Services.Constants;
using MaIN.Services.Services.Abstract;
using MaIN.Services.Services.LLMService.Auth;
using MaIN.Services.Services.LLMService.Memory;
using MaIN.Services.Services.Models;
using MaIN.Services.Utils;
using Microsoft.Extensions.Logging;

namespace MaIN.Services.Services.LLMService;

public sealed class VertexService(
    MaINSettings settings,
    INotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    IMemoryFactory memoryFactory,
    IMemoryService memoryService,
    ILogger<VertexService>? logger = null)
    : OpenAiCompatibleService(notificationService, httpClientFactory, memoryFactory, memoryService, logger), ILLMService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private GoogleServiceAccountTokenProvider? _tokenProvider;
    private string _location = "us-central1";

    private GoogleServiceAccountConfig Auth
        => settings.GoogleServiceAccountAuth
           ?? throw new InvalidOperationException("Vertex AI service account is not configured.");

    protected override string HttpClientName => ServiceConstants.HttpClients.VertexClient;

    protected override string ChatCompletionsUrl
        => $"https://{_location}-aiplatform.googleapis.com/v1beta1/projects/{Auth.ProjectId}/locations/{_location}/endpoints/openapi/chat/completions";

    protected override string ModelsUrl
        => $"https://{_location}-aiplatform.googleapis.com/v1beta1/projects/{Auth.ProjectId}/locations/{_location}/endpoints/openapi/models";

    protected override Type ExpectedParamsType => typeof(VertexInferenceParams);

    protected override string GetApiKey()
    {
        var auth = Auth;
        _tokenProvider ??= new GoogleServiceAccountTokenProvider(auth);

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        // Task.Run avoids deadlocking on Blazor Server's single-threaded SynchronizationContext
        return Task.Run(() => _tokenProvider.GetAccessTokenAsync(httpClient)).GetAwaiter().GetResult();
    }

    protected override string GetApiName() => LLMApiRegistry.Vertex.ApiName;

    protected override void ValidateApiKey()
    {
        var auth = Auth;
        if (string.IsNullOrEmpty(auth.ProjectId))
        {
            throw new InvalidOperationException("GoogleServiceAccountConfig.ProjectId is required.");
        }

        if (string.IsNullOrEmpty(auth.ClientEmail))
        {
            throw new InvalidOperationException("GoogleServiceAccountConfig.ClientEmail is required.");
        }

        if (string.IsNullOrEmpty(auth.PrivateKey))
        {
            throw new InvalidOperationException("GoogleServiceAccountConfig.PrivateKey is required.");
        }
    }

    protected override void ApplyBackendParams(Dictionary<string, object> requestBody, Chat chat)
    {
        if (chat.BackendParams is not VertexInferenceParams p)
        {
            return;
        }

        if (p.Temperature.HasValue)
        {
            requestBody["temperature"] = p.Temperature.Value;
        }

        if (p.MaxTokens.HasValue)
        {
            requestBody["max_tokens"] = p.MaxTokens.Value;
        }

        if (p.TopP.HasValue)
        {
            requestBody["top_p"] = p.TopP.Value;
        }

        if (p.StopSequences is { Length: > 0 })
        {
            requestBody["stop"] = p.StopSequences;
        }
    }

    public override async Task<ChatResult?> Send(
        Chat chat,
        ChatRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ExtractLocation(chat);
        return await base.Send(chat, options, cancellationToken);
    }

    private protected override async Task<IIngestedMemory> CreateIngestedMemoryAsync(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        CancellationToken ct = default)
    {
        var auth = Auth;
        _tokenProvider ??= new GoogleServiceAccountTokenProvider(auth);
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var tokenProvider = _tokenProvider;

        var kernel = _memoryFactory.CreateMemoryWithVertex(
            () => new ValueTask<string>(Task.Run(() => tokenProvider.GetAccessTokenAsync(httpClient))),
            _location,
            auth.ProjectId,
            chat.MemoryParams);

        await _memoryService.ImportDataToMemory((kernel, null), memoryOptions, ct);
        return new IngestedMemory(kernel, () => kernel.DeleteIndexAsync());
    }

    /// <summary>
    /// Sends files directly to Gemini via multimodal API (bypasses KernelMemory).
    /// PDFs and images are sent inline (Gemini handles OCR natively),
    /// other formats are pre-processed to text via DocumentProcessor.
    /// </summary>
    public override async Task<ChatResult?> AskMemory(
        Chat chat,
        ChatMemoryOptions memoryOptions,
        ChatRequestOptions requestOptions,
        CancellationToken cancellationToken = default)
    {
        ExtractLocation(chat);

        if (!chat.Messages.Any())
        {
            return null;
        }

        var lastMessage = chat.Messages.Last();
        var originalContent = lastMessage.Content;
        var originalFiles = lastMessage.Files;
        var originalImages = lastMessage.Images;

        try
        {
            var inlineBytes = new List<byte[]>();
            var textContext = new StringBuilder();

            CollectTextData(memoryOptions, textContext);
            await CollectFilesData(memoryOptions, inlineBytes, textContext, cancellationToken);
            await CollectStreamData(memoryOptions, inlineBytes, textContext, cancellationToken);
            CollectMemoryItems(memoryOptions, textContext);

            lastMessage.Content = BuildQuery(originalContent, textContext, chat.MemoryParams.Grammar);
            lastMessage.Files = null;
            lastMessage.Images = MergeInlineContent(originalImages, inlineBytes);

            return await Send(chat, requestOptions, cancellationToken);
        }
        finally
        {
            lastMessage.Content = originalContent;
            lastMessage.Files = originalFiles;
            lastMessage.Images = originalImages;
        }
    }

    #region Multimodal File Processing

    private static readonly HashSet<string> NativeMultimodalExtensions =
        [".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif"];

    private static bool IsNativeMultimodalFile(string fileName)
        => NativeMultimodalExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private static void CollectTextData(ChatMemoryOptions options, StringBuilder textContext)
    {
        foreach (var (name, content) in options.TextData)
        {
            AppendDocument(textContext, name, content);
        }
    }

    private static async Task CollectFilesData(
        ChatMemoryOptions options, List<byte[]> inlineBytes, StringBuilder textContext,
        CancellationToken cancellationToken)
    {
        foreach (var (name, path) in options.FilesData)
        {
            if (IsNativeMultimodalFile(name))
            {
                inlineBytes.Add(await File.ReadAllBytesAsync(path, cancellationToken));
            }
            else
            {
                AppendDocument(textContext, name, DocumentProcessor.ProcessDocument(path));
            }
        }
    }

    private static async Task CollectStreamData(
        ChatMemoryOptions options, List<byte[]> inlineBytes, StringBuilder textContext,
        CancellationToken cancellationToken)
    {
        foreach (var (name, stream) in options.StreamData)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();

            if (IsNativeMultimodalFile(name))
            {
                inlineBytes.Add(bytes);
            }
            else
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"vertex_{Guid.NewGuid()}{Path.GetExtension(name)}");
                try
                {
                    await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
                    AppendDocument(textContext, name, DocumentProcessor.ProcessDocument(tempPath));
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
        }
    }

    private static void CollectMemoryItems(ChatMemoryOptions options, StringBuilder textContext)
    {
        if (options.Memory is not { Count: > 0 })
        {
            return;
        }

        foreach (var item in options.Memory)
        {
            textContext.AppendLine(item);
            textContext.AppendLine();
        }
    }

    private static void AppendDocument(StringBuilder sb, string name, string content)
    {
        sb.AppendLine($"[Document: {name}]");
        sb.AppendLine(content);
        sb.AppendLine();
    }

    private static string BuildQuery(string userQuestion, StringBuilder documentContext, Grammar? grammar)
    {
        var query = new StringBuilder();
        if (documentContext.Length > 0)
        {
            query.AppendLine("Use the following document content to answer the question:\n");
            query.Append(documentContext);
            query.AppendLine();
        }

        query.Append(userQuestion);

        if (grammar != null)
        {
            var jsonGrammar = new GrammarToJsonConverter().ConvertToJson(grammar);
            query.Append(
                $" | For your next response only, please respond using exactly the following JSON format: \n{jsonGrammar}\n. Do not include any explanations, code blocks, or additional content. After this single JSON response, resume your normal conversational style.");
        }

        return query.ToString();
    }

    private static List<byte[]>? MergeInlineContent(List<byte[]>? existingImages, List<byte[]> newBytes)
    {
        if ((existingImages == null || existingImages.Count == 0) && newBytes.Count == 0)
        {
            return null;
        }

        var merged = new List<byte[]>(existingImages ?? []);
        merged.AddRange(newBytes);
        return merged;
    }

    #endregion

    private void ExtractLocation(Chat chat)
    {
        if (chat.BackendParams is VertexInferenceParams vp)
        {
            _location = vp.Location;
        }
    }
}
