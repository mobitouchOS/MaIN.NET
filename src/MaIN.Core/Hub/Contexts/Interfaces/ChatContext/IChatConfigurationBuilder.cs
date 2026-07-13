using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Models;
using MaIN.Services.Services.Models;
using FileInfo = MaIN.Domain.Entities.FileInfo;

namespace MaIN.Core.Hub.Contexts.Interfaces.ChatContext;

public interface IChatConfigurationBuilder : IChatActions
{
    /// <summary>
    /// Sets the inference parameters for the chat session, allowing you to customize how the AI processes and generates
    /// responses based on specific parameters. Inference parameters can influence various aspects of the chat, such as response length,
    /// temperature, and other model-specific settings.
    /// </summary>
    /// <param name="inferenceParams">An <see cref="IBackendInferenceParams"/> object that holds the parameters for inference, such as Temperature,
    /// MaxTokens, TopP, etc. These parameters control the generation behavior of the chat.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithInferenceParams(IBackendInferenceParams inferenceParams);

    /// <summary>
    /// Attaches external tools/functions that the model can invoke during the conversation.
    /// </summary>
    /// <param name="toolsConfiguration"> A <see cref="ToolsConfiguration"/> - configuration defining available tools
    /// and their execution modes.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithTools(ToolsConfiguration toolsConfiguration);

    /// <summary>
    /// Configures the chat session to stop immediately when the model emits a tool call,
    /// returning the requested tool calls to the caller without executing them server-side.
    /// Useful for OpenAI-compatible API endpoints or clients that execute tools locally.
    /// </summary>
    /// <param name="enabled">True to enable client-side tool execution mode; false for default server-side loop.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithClientSideToolExecution(bool enabled = true);

    /// <summary>
    /// Sets the memory parameters for the chat session, allowing you to customize how the AI accesses and
    /// uses its memory for generating responses. Memory parameters influence aspects such as context size, memory search depth,
    /// and token allocation for responses.
    /// </summary>
    /// <param name="memoryParams">A <see cref="MemoryParams"/> object that holds the parameters for memory management, such as ContextSize,
    /// MaxMatchesCount, AnswerTokens, etc. These parameters control how the chat uses memory for response generation.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithMemoryParams(MemoryParams memoryParams);

    /// <summary>
    /// Configures the session to use Text-to-Speech for the model's responses.
    /// </summary>
    /// <param name="speechParams">A <see cref="TextToSpeechParams"/> - parameters for the voice synthesis.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder Speak(TextToSpeechParams speechParams);

    /// <summary>
    /// Inserts a system message at the beginning of the chat. System messages are typically used for setting the context
    /// or providing instructions to the AI.
    /// </summary>
    /// <param name="systemPrompt">The system prompt content that provides instructions to the AI.</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithSystemPrompt(string systemPrompt);

    /// <summary>
    /// Attaches files to the most recent message in the chat. Files are associated with the last message to provide additional context
    /// or media for the AI to process.
    /// </summary>
    /// <param name="file">A list of <see cref="FileStream"/> objects representing the files to attach.</param>
    /// <param name="preProcess">Include preprocessing of a document that can consume more time and resources
    /// but can also greatly improve the quality of inference</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithFiles(List<FileStream> file, bool preProcess = false);

    /// <summary>
    /// Attaches files to the most recent message in the chat. Files are associated with the last message to provide additional context
    /// or media for the AI to process.
    /// </summary>
    /// <param name="file">A list of <see cref="FileInfo"/> objects representing the files to attach.</param>
    /// <param name="preProcess">Include preprocessing of a document that can consume more time and resources
    /// but can also greatly improve the quality of inference</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithFiles(List<FileInfo> file, bool preProcess = false);

    /// <summary>
    /// Attaches a list of files to the most recent message in the chat by specifying their file paths.
    /// This method is an alternative to using FileInfo.
    /// </summary>
    /// <param name="file">A list of file paths to attach to the most recent message.</param>
    /// <param name="preProcess">Include preprocessing of a document that can consume more time and resources
    /// but can also greatly improve the quality of inference</param>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder WithFiles(List<string> file, bool preProcess = false);

    /// <summary>
    /// Each time we run inference, we need to load the model into memory; this takes time and memory. This method allows us to save some
    /// more of GPU/RAM resources at the cost of time, because model weights are no longer cached
    /// </summary>
    /// <returns>The context instance implementing <see cref="IChatConfigurationBuilder"/> for method chaining.</returns>
    IChatConfigurationBuilder DisableCache();

    /// <summary>
    /// Completes the chat session by generating a response based on the messages so far. This method interacts with the underlying
    /// chat service to process the chat and generate a result.
    /// </summary>
    /// <param name="translate">A flag indicating whether the response should be translated. Default is false.</param>
    /// <param name="interactive">A flag indicating whether the chat session should be interactive. Default is false.</param>
    /// <param name="changeOfValue">An optional callback invoked whenever a new token or update is received during streaming.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <param name="toolCallback">An optional callback invoked when the model requests a tool call, before it executes.</param>
    /// <returns>A <see cref="ChatResult"/> object containing the result of the completed chat session.</returns>
    Task<ChatResult> CompleteAsync(bool translate = false, bool interactive = false, Func<LLMTokenValue?, Task>? changeOfValue = null, CancellationToken cancellationToken = default, Func<ToolInvocation, Task>? toolCallback = null);
}
