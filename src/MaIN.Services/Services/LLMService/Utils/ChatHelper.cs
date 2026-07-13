using System.Text.Json;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.Services.Constants;
using MaIN.Services.Utils;

namespace MaIN.Services.Services.LLMService.Utils;

/// <summary>
/// Helper class for chat-related operations
/// </summary>
public static class ChatHelper
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif"
    ];

    /// <summary>
    /// Extracts image files from message.Files into message.Images and removes them from Files.
    /// This must be called before HasFiles() so images are not mistakenly routed to the RAG/memory path.
    /// </summary>
    public static async Task ExtractImageFromFiles(Message message)
    {
        if (message.Files == null || message.Files.Count == 0)
            return;

        var imageFiles = message.Files
            .Where(f => ImageExtensions.Contains(f.Extension.ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
            return;

        var imageBytesList = new List<byte[]>();
        foreach (var imageFile in imageFiles)
        {
            if (imageFile.StreamContent != null)
            {
                using var ms = new MemoryStream();
                imageFile.StreamContent.Position = 0;
                await imageFile.StreamContent.CopyToAsync(ms);
                imageBytesList.Add(ms.ToArray());
            }
            else if (imageFile.Path != null)
            {
                imageBytesList.Add(await File.ReadAllBytesAsync(imageFile.Path));
            }

            message.Files.Remove(imageFile);
        }

        message.Images = imageBytesList;

        if (message.Files.Count == 0)
            message.Files = null;
    }


    /// <summary>
    /// Checks if a message contains files
    /// </summary>
    public static bool HasFiles(Message message)
    {
        return message.Files?.Any() ?? false;
    }

    public static bool HasImages(Message message)
    {
        return message.Images?.Count > 0;
    }

    /// <summary>
    /// Extracts memory options from a message with files
    /// </summary>
    public static ChatMemoryOptions ExtractMemoryOptions(Message message)
    {
        if (message.Files == null)
            return new ChatMemoryOptions();
            
        var textData = message.Files
            .Where(x => x.Content != null)
            .ToDictionary(x => x.Name, x => x.Content!);
        
        var fileData = message.Files
            .Where(x => x.Path != null)
            .ToDictionary(x => x.Name, x => x.Path!);
        
        var streamData = message.Files
            .Where(x => x.StreamContent != null)
            .ToDictionary(x => x.Name, x => x.StreamContent!);

        var preProcess = message.Properties.CheckProperty(ServiceConstants.Properties.PreProcessProperty);
        
        return new ChatMemoryOptions
        {
            TextData = textData,
            FilesData = fileData,
            StreamData = streamData,
            PreProcess = preProcess
        };
    }

    /// <summary>
    /// Builds an array of message objects for API requests, handling images and grammar injection.
    /// </summary>
    internal static void MergeMessages(List<ChatMessage> conversation, List<Message> messages)
    {
        var existing = new HashSet<(string, object)>(conversation.Select(m => (m.Role, m.Content)));
        foreach (var msg in messages)
        {
            var role = msg.Role.ToLowerInvariant();

            if (HasImages(msg))
            {
                var simplifiedContent = $"{msg.Content} [Contains image]";
                if (!existing.Contains((role, simplifiedContent)))
                {
                    var chatMessage = new ChatMessage(role, msg.Content) { OriginalMessage = msg };
                    conversation.Add(chatMessage);
                    existing.Add((role, simplifiedContent));
                }
            }
            else
            {
                if (!existing.Contains((role, msg.Content)))
                {
                    var chatMessage = new ChatMessage(role, msg.Content);

                    if (msg.Properties.ContainsKey(ServiceConstants.Properties.ToolCallsProperty))
                    {
                        var toolCallsJson = msg.Properties[ServiceConstants.Properties.ToolCallsProperty];
                        chatMessage.ToolCalls = JsonSerializer.Deserialize<List<ToolCall>>(toolCallsJson);
                    }

                    if (msg.Properties.ContainsKey(ServiceConstants.Properties.ToolCallIdProperty))
                        chatMessage.ToolCallId = msg.Properties[ServiceConstants.Properties.ToolCallIdProperty];

                    if (msg.Properties.ContainsKey(ServiceConstants.Properties.ToolNameProperty))
                        chatMessage.Name = msg.Properties[ServiceConstants.Properties.ToolNameProperty];

                    conversation.Add(chatMessage);
                    existing.Add((role, msg.Content));
                }
            }
        }
    }

    internal static async Task<object[]> BuildMessagesArray(List<ChatMessage> conversation, Chat chat, ImageType imageType)
    {
        var messages = new List<object>();

        foreach (var msg in conversation)
        {
            var content = msg.OriginalMessage != null ? BuildMessageContent(msg.OriginalMessage, imageType) : msg.Content;
            if (chat.InferenceGrammar != null && msg.Role == "user")
            {
                var jsonGrammarConverter = new GrammarToJsonConverter();
                string jsonGrammar = jsonGrammarConverter.ConvertToJson(chat.InferenceGrammar);

                var grammarInstruction = $" | Respond only using the following JSON format: \n{jsonGrammar}\n. Do not add explanations, code tags, or any extra content.";

                if (content is string textContent)
                {
                    content = textContent + grammarInstruction;
                }
                else if (content is List<object> contentParts)
                {
                    var modifiedParts = contentParts.ToList();
                    modifiedParts.Add(new { type = "text", text = grammarInstruction });
                    content = modifiedParts;
                }
            }

            var messageObj = new Dictionary<string, object>
            {
                ["role"] = msg.Role,
                ["content"] = content ?? string.Empty
            };

            if (msg.ToolCalls != null && msg.ToolCalls.Any())
            {
                messageObj["tool_calls"] = msg.ToolCalls;
            }

            if (!string.IsNullOrEmpty(msg.ToolCallId))
            {
                messageObj["tool_call_id"] = msg.ToolCallId;

                if (!string.IsNullOrEmpty(msg.Name))
                {
                    messageObj["name"] = msg.Name;
                }
            }

            messages.Add(messageObj);
        }

        return messages.ToArray();
    }

    private static object BuildMessageContent(Message message, ImageType imageType)
    {
        if (message.Images == null || message.Images.Count == 0)
        {
            return message.Content;
        }

        var contentParts = new List<object>();

        if (!string.IsNullOrEmpty(message.Content))
        {
            contentParts.Add(new
            {
                type = "text",
                text = message.Content
            });
        }

        foreach (var imageBytes in message.Images)
        {
            var base64Data = Convert.ToBase64String(imageBytes);
            var mimeType = DetectImageMimeType(imageBytes);

            switch (imageType)
            {
                case ImageType.AsUrl:
                    contentParts.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:{mimeType};base64,{base64Data}",
                            detail = "auto"
                        }
                    });
                    break;
                case ImageType.AsBase64:
                    contentParts.Add(new
                    {
                        type = "image",
                        source = new
                        {
                            data = base64Data,
                            media_type = mimeType,
                            type = "base64"
                        }
                    });
                    break;
            }
        }

        return contentParts;
    }

    private static string DetectImageMimeType(byte[] imageBytes)
    {
        if (imageBytes.Length < 4)
            return "image/jpeg";

        // PDF: %PDF (0x25 0x50 0x44 0x46)
        if (imageBytes[0] == 0x25 && imageBytes[1] == 0x50 &&
            imageBytes[2] == 0x44 && imageBytes[3] == 0x46)
            return "application/pdf";

        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
            return "image/jpeg";

        if (imageBytes.Length >= 8 &&
            imageBytes[0] == 0x89 && imageBytes[1] == 0x50 &&
            imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            return "image/png";

        if (imageBytes.Length >= 6 &&
            imageBytes[0] == 0x47 && imageBytes[1] == 0x49 &&
            imageBytes[2] == 0x46 && imageBytes[3] == 0x38)
            return "image/gif";

        if (imageBytes.Length >= 12 &&
            imageBytes[0] == 0x52 && imageBytes[1] == 0x49 &&
            imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
            imageBytes[8] == 0x57 && imageBytes[9] == 0x45 &&
            imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
            return "image/webp";

        if (imageBytes.Length >= 12 &&
            imageBytes[4] == 0x66 && imageBytes[5] == 0x74 &&
            imageBytes[6] == 0x79 && imageBytes[7] == 0x70)
        {
            if ((imageBytes[8] == 0x68 && imageBytes[9] == 0x65 && imageBytes[10] == 0x69 && imageBytes[11] == 0x63) ||
                (imageBytes[8] == 0x68 && imageBytes[9] == 0x65 && imageBytes[10] == 0x69 && imageBytes[11] == 0x66))
                return "image/heic";
        }

        if (imageBytes.Length >= 12 &&
            imageBytes[4] == 0x66 && imageBytes[5] == 0x74 &&
            imageBytes[6] == 0x79 && imageBytes[7] == 0x70 &&
            imageBytes[8] == 0x61 && imageBytes[9] == 0x76 &&
            imageBytes[10] == 0x69 && imageBytes[11] == 0x66)
            return "image/avif";

        return "image/jpeg";
    }
}