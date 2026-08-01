using MaIN.Core;
using MaIN.Core.Hub;
using MaIN.Domain.Configuration;
using MaIN.Domain.Models.Concrete;
using MaIN.Domain.Models.Abstract;
using MaIN.InferPage.Endpoints;
using MaIN.InferPage.Middlewares;
using MaIN.InferPage.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.FluentUI.AspNetCore.Components;
using MaIN.InferPage.Components;
using Scalar.AspNetCore;
using Utils = MaIN.InferPage.Utils;

// Reads --flagName from the raw CLI args first (unambiguous — nothing accidentally passes this
// as a process argument), falling back to a plain environment variable. envVarName is
// deliberately a different string than flagName for "path": builder.Configuration["path"] would
// also match the OS's PATH environment variable (case-insensitive matching), silently feeding
// the system search path into Directory.CreateDirectory(...) below. Environment.GetEnvironmentVariable
// only matches the exact name given, so "modelPath" cannot collide with "PATH".
static string? GetExplicitCliOrEnvValue(string[] args, string flagName, string envVarName)
{
    var equalsPrefix = $"--{flagName}=";
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return args[i][equalsPrefix.Length..];
        }

        if (string.Equals(args[i], $"--{flagName}", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return Environment.GetEnvironmentVariable(envVarName);
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SettingsStateService>();
builder.Services.AddScoped<AgentsPanelStateService>();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ApiLogService>();
builder.Services.AddSingleton<AgentDefinitionService>();
builder.Services.AddSingleton<AgentRunner>();

if (!builder.Environment.IsDevelopment())
{
    // Persist Data Protection keys so antiforgery tokens survive container restarts.
    // Mount /app/DataProtection-Keys as a volume to keep keys across container recreations.
    builder.Services.AddDataProtection()
        .SetApplicationName("MaIN.InferPage")
        .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtection-Keys"));
}

try
{
    var modelArg = builder.Configuration["model"];
    var modelPathArg = GetExplicitCliOrEnvValue(args, "path", "modelPath");
    var backendArg = builder.Configuration["backend"];
    var modelUrlArg = builder.Configuration["modelUrl"];

    bool hasCommandLineConfig = backendArg != null || modelArg != null;

    if (hasCommandLineConfig)
    {
        if (backendArg != null)
        {
            Utils.BackendType = backendArg.ToLower() switch
            {
                "openai" => BackendType.OpenAi,
                "gemini" => BackendType.Gemini,
                "deepseek" => BackendType.DeepSeek,
                "groqcloud" => BackendType.GroqCloud,
                "anthropic" => BackendType.Anthropic,
                "xai" => BackendType.Xai,
                "ollama" => BackendType.Ollama,
                "vertex" => BackendType.Vertex,
                _ => BackendType.Self
            };

            if (Utils.BackendType == BackendType.Vertex)
            {
                Console.WriteLine("Vertex AI requires service account credentials. Configure them via the Settings page.");
            }
            else if (Utils.BackendType != BackendType.Self)
            {
                var apiKeyVariable = LLMApiRegistry.GetEntry(Utils.BackendType)?.ApiKeyEnvName ?? string.Empty;
                var key = Environment.GetEnvironmentVariable(apiKeyVariable);

                if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(apiKeyVariable))
                {
                    Console.Write($"Please enter your {Utils.BackendType.ToString()} API key: ");
                    key = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        Utils.HasApiKey = true;
                        Environment.SetEnvironmentVariable(apiKeyVariable, key);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(modelArg))
        {
            Utils.Model = modelArg;
            Utils.Path = modelPathArg;

            if (Utils.BackendType == BackendType.Self)
            {
                if (string.IsNullOrEmpty(modelPathArg))
                {
                    var defaultPath = Utils.DefaultModelsPath;
                    Directory.CreateDirectory(defaultPath);
                    Environment.SetEnvironmentVariable("MaIN_ModelsPath", defaultPath);
                    Utils.Path = defaultPath;
                    Console.WriteLine($"No --path provided. Using default models directory: {defaultPath}");
                }
                else
                {
                    var modelsDirectory = modelPathArg.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                        ? System.IO.Path.GetDirectoryName(modelPathArg) ?? Utils.DefaultModelsPath
                        : modelPathArg;
                    Directory.CreateDirectory(modelsDirectory);
                    Environment.SetEnvironmentVariable("MaIN_ModelsPath", modelsDirectory);
                }

                if (!ModelRegistry.Exists(modelArg) && !string.IsNullOrEmpty(modelUrlArg))
                {
                    var fileName = Utils.SanitizeModelFileName(modelArg);
                    var downloadUrl = new Uri(modelUrlArg);
                    ModelRegistry.RegisterOrReplace(new GenericLocalModel(
                        FileName: fileName,
                        Id: modelArg,
                        DownloadUrl: downloadUrl));
                    // Log only scheme+host+path -- modelUrl may carry a SAS/presigned token in its
                    // query string, which must never land in console/container logs.
                    Console.WriteLine($"Registered custom model '{modelArg}' for download from {downloadUrl.GetLeftPart(UriPartial.Path)}");
                }
            }
        }
        else
        {
            Console.WriteLine("No model argument provided. Continuing without model configuration.");
        }
    }
    else
    {
        // No CLI args — settings will be loaded from browser localStorage
        Utils.NeedsConfiguration = true;
    }
}
catch (Exception ex)
{
    Console.WriteLine("Error during parameter processing: " + ex.Message);
    return;
}

// For Self backend CLI mode, validate model before registering.
// Utils.Path is always set by this point for Self + a model arg (see above), so the previous
// "Utils.Path == null" clause never actually triggered this guard — dropped so an unrecognized
// model name (with no modelUrl to register it) is now caught here instead of failing later.
if (!Utils.NeedsConfiguration && Utils.BackendType == BackendType.Self
    && !ModelRegistry.Exists(Utils.Model!))
{
    Console.WriteLine($"Model: {Utils.Model} is not supported");
    Environment.Exit(0);
}

if (!Utils.NeedsConfiguration && Utils.BackendType != BackendType.Self)
    builder.Services.AddMaIN(builder.Configuration, s => s.BackendType = Utils.BackendType);
else
    // NeedsConfiguration or Self backend: register with defaults
    builder.Services.AddMaIN(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseMiddleware<ApiLoggingMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();
app.Services.UseMaIN();
AIHub.Extensions.DisableLLamaLogs();

if (!Utils.NeedsConfiguration && Utils.BackendType == BackendType.Self && !string.IsNullOrEmpty(Utils.Model))
{
    var initialModel = Utils.Model;
    _ = Task.Run(async () =>
    {
        try
        {
            if (!AIHub.Model().Exists(initialModel))
            {
                Console.WriteLine($"Downloading model '{initialModel}'...");
                await AIHub.Model().EnsureDownloadedAsync(initialModel);
                Console.WriteLine($"Model '{initialModel}' is ready.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download model '{initialModel}': {ex.Message}");
        }
    });
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
OpenAiCompatEndpoints.InitializeModelCache();
app.MapOpenAiCompatEndpoints();

// Interactive API docs for the OpenAI-compatible endpoint (/scalar/v1), backed by the
// standard ASP.NET Core OpenAPI document at /openapi/v1.json -- exposed unconditionally
// (not just in Development) since self-hosted InferPage instances are commonly run in
// Docker/Production and this is the primary way an operator tests the API by hand.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("MaIN.InferAPI")
        .WithTheme(ScalarTheme.BluePlanet)
        .ForceDarkMode()
        .HideSearch()
        .ExpandAllTags()
        .SortTagsAlphabetically()
        .SortOperationsByMethod()
        .PreserveSchemaPropertyOrder()
        .AddServer("http://localhost:5555", "Local")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddHeadContent("""
            <style>
              .scalar-api-reference { --scalar-font: 'Inter', system-ui, sans-serif; }
              .scalar-api-reference-header { background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%) !important; }
              .scalar-api-reference-header h1 { color: #e94560 !important; font-weight: 800 !important; }
            </style>
            """)
        .AddHeaderContent("""
            <div style="padding: 10px 20px; background: #0d1117; border-bottom: 1px solid #30363d; color: #c9d1d9; font-family: system-ui, sans-serif; font-size: 12px; line-height: 1.5;">
              <div style="max-width: 960px; margin: 0 auto;">
                <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 6px;">
                  <span style="background: #e94560; color: #fff; padding: 2px 6px; border-radius: 4px; font-weight: 700; font-size: 10px; text-transform: uppercase;">MaIN.InferAPI</span>
                  <span style="color: #8b949e;">OpenAI-compatible local inference — structured output, tool calling, multi-model</span>
                </div>
                <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px;">
                  <div style="background: #161b22; border: 1px solid #30363d; border-radius: 4px; padding: 6px 8px;">
                    <div style="font-weight: 600; color: #58a6ff; font-size: 11px;">Structured Output</div>
                    <div style="color: #8b949e; font-size: 11px;"><code style="background:#1f2937;padding:0 3px;border-radius:2px;">response_format</code> + <code style="background:#1f2937;padding:0 3px;border-radius:2px;">json_schema</code></div>
                  </div>
                  <div style="background: #161b22; border: 1px solid #30363d; border-radius: 4px; padding: 6px 8px;">
                    <div style="font-weight: 600; color: #58a6ff; font-size: 11px;">Tool Calling</div>
                    <div style="color: #8b949e; font-size: 11px;">7 hosted tools — web_search, http_request, etc.</div>
                  </div>
                  <div style="background: #161b22; border: 1px solid #30363d; border-radius: 4px; padding: 6px 8px;">
                    <div style="font-weight: 600; color: #58a6ff; font-size: 11px;">Dynamic Loading</div>
                    <div style="color: #8b949e; font-size: 11px;">Drop GGUF files, switch via <code style="background:#1f2937;padding:0 3px;border-radius:2px;">model</code> field</div>
                  </div>
                </div>
              </div>
            </div>
            """);
});

app.Run();