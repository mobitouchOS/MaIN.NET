using MaIN.Core.Hub;
using MaIN.Domain.Models;

namespace MaIN.Core.E2ETests;

[Collection("E2ETests")]
public class ModelDownloadTests : IntegrationTestBase
{
    private static readonly string[] ModelsUsedInTests =
    [
        Models.Local.Qwen2_5_0_5b,
        Models.Local.Gemma2_2b,
        Models.Local.Llama3_2_3b,
        Models.Local.Gemma3_4b,
        Models.Local.MxbaiEmbedding,
    ];

    [SkippableFact]
    public async Task Should_DownloadModels_UsedByE2ETests()
    {
        foreach (var modelId in ModelsUsedInTests)
        {
            await AIHub.Model().EnsureDownloadedAsync(modelId);
        }
    }
}
