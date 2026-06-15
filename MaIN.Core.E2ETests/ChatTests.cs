using FuzzySharp;
using MaIN.Core.E2ETests.Helpers;
using MaIN.Core.Hub;
using MaIN.Domain.Entities;
using MaIN.Domain.Models;
using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;

namespace MaIN.Core.E2ETests;

[Collection("E2ETests")]
public class ChatTests : IntegrationTestBase
{
    public ChatTests() : base()
    {
    }

    [Fact]
    public async Task Should_AnswerQuestion_BasicChat()
    {
        var context = AIHub.Chat().WithModel(Models.Local.Qwen2_5_0_5b);

        var result = await context
            .WithMessage("Where the hedgehog goes at night?")
            .CompleteAsync(interactive: true);

        Assert.True(result.Done);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message.Content);
    }

    [Fact]
    public async Task Should_AnswerFileSubject_ChatWithFiles()
    {
        List<string> files = ["./Files/Nicolaus_Copernicus.pdf"];

        var result = await AIHub.Chat()
            .WithModel(Models.Local.Qwen2_5_0_5b)
            .WithMessage("Who is described in the file? Reply with ONLY their full name. No explanation, no punctuation. Example: Isaak Newton")
            .WithMemoryParams(new MemoryParams { AnswerTokens = 10 })
            .WithFiles(files)
            .CompleteAsync();

        Assert.True(result.Done);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message.Content);
        var ratio = Fuzz.PartialRatio("nicolaus copernicus", result.Message.Content.ToLowerInvariant());
        Assert.True(ratio > 50,
            $"""
            Fuzzy match failed!
            Expected > 50, but got {ratio}.
            Expected: 'nicolaus copernicus'
            Actual: '{result.Message.Content}'
            """);
    }

    [Fact]
    public async Task Should_AnswerQuestion_FromExistingChat()
    {
        var result = AIHub.Chat()
            .WithModel(Models.Local.Qwen2_5_0_5b);

        await result.WithMessage("What do you think about math theories?")
            .WithMemoryParams(new MemoryParams { AnswerTokens = 10 })
            .CompleteAsync();

        await result.WithMessage("And about physics?")
            .CompleteAsync();

        var chatNewContext = await AIHub.Chat().FromExisting(result.GetChatId());
        var messages = chatNewContext.GetChatHistory();

        Assert.Equal(4, messages.Count);
    }

    [Fact]
    public async Task Should_AnswerGameFromImage_ChatWithImagesWithText()
    {
        List<string> images = ["./Files/gamex.jpg"];
        var expectedAnswer = "call of duty";

        var result = await AIHub.Chat()
            .WithModel(Models.Local.Llama3_2_3b)
            .WithMessage("What is the title of the game? Answer in 3 words.")
            .WithMemoryParams(new MemoryParams { AnswerTokens = 10 })
            .WithFiles(images)
            .CompleteAsync();

        Assert.True(result.Done);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message.Content);
        var ratio = Fuzz.PartialRatio(expectedAnswer, result.Message.Content.ToLowerInvariant());
        Assert.True(ratio > 50,
            $"""
            Fuzzy match failed!
            Expected > 50, but got {ratio}.
            Expexted: '{expectedAnswer}'
            Actual: '{result.Message.Content}'
            """);
    }

    [Fact]
    public async Task Should_AnswerAppleFromImage_ChatWithImagesWithVision()
    {
        List<string> images = ["./Files/apple.jpg"];
        var expectedAnswer = "apple";

        var result = await AIHub.Chat()
            .WithModel(Models.Local.Gemma3_4b)
            .WithMessage("What is this fruit? Answer in one word.")
            .WithMemoryParams(new MemoryParams { AnswerTokens = 10 })
            .WithFiles(images)
            .CompleteAsync();

        Assert.True(result.Done);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message.Content);
        var ratio = Fuzz.PartialRatio(expectedAnswer, result.Message.Content.ToLowerInvariant());
        Assert.True(ratio > 50,
            $"""
            Fuzzy match failed!
            Expected > 50, but got {ratio}.
            Expexted: '{expectedAnswer}'
            Actual: '{result.Message.Content}'
            """);
    }

    [Fact(Skip = "Require powerful GPU")]
    public async Task Should_GenerateImage_BasedOnPrompt()
    {
        ModelRegistry.RegisterOrReplace(new StableDiffusion1_5());

        var result = await AIHub.Chat()
            .WithModel(Models.Local.StableDiffusion1_5)
            .WithMessage("Generate cat in Rome. Sightseeing, colloseum, ancient builidngs, Italy.")
            .CompleteAsync();

        Assert.True(result.Done);
        Assert.NotNull(result.Message.Image);
    }

    [Fact]
    public async Task Should_AnswerFileSubject_ChatWithFiles_UsingStreams()
    {
        List<string> files = ["./Files/Nicolaus_Copernicus.pdf"];

        var fileStreams = new List<FileStream>();

        foreach (var path in files)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            fileStreams.Add(fs);
        }

        var expectedAnswer = "nicolaus copernicus";

        var result = await AIHub.Chat()
            .WithModel(Models.Local.Qwen2_5_0_5b)
            .WithMessage("Who is described in the file? Reply with ONLY their full name. No explanation, no punctuation. Example: Isaak Newton")
            .WithMemoryParams(new MemoryParams { AnswerTokens = 10 })
            .WithFiles(fileStreams)
            .CompleteAsync();

        Assert.True(result.Done);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message.Content);
        var ratio = Fuzz.PartialRatio(expectedAnswer, result.Message.Content.ToLowerInvariant());
        Assert.True(ratio > 50,
            $"""
            Fuzzy match failed!
            Expected > 50, but got {ratio}.
            Expected: '{expectedAnswer}'
            Actual: '{result.Message.Content}'
            """);
    }
}
