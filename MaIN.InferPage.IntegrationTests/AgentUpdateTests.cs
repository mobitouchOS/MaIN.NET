using MaIN.Core;
using MaIN.InferPage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MaIN.InferPage.IntegrationTests;

// AIHub/ModelRegistry are static, process-wide state — join the shared collection so this runs
// sequentially with the other tests that touch them.
[Collection("InferPageEndpointTests")]
public class AgentUpdateTests
{
    [Fact]
    public async Task UpdateAsync_preserves_id_and_applies_new_name_and_tools_without_orphaning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "main-agent-update-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaIN:FileSystemSettings:Path"] = tempDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMaIN(config);
        await using var sp = services.BuildServiceProvider();
        sp.UseMaIN();

        var svc = new AgentDefinitionService(sp.GetRequiredService<IHttpClientFactory>(), config);

        try
        {
            var created = await svc.CreateAsync(new CreateAgentRequest("Before", "gpt-4o", "You are v1.", []));
            var id = created.Id;

            var updated = await svc.UpdateAsync(id, new CreateAgentRequest("After", "gpt-4o", "You are v2.", ["web_search"]));

            // id preserved (so agent:<id> refs / active-agent selection survive an edit)
            Assert.Equal(id, updated.Id);
            Assert.Equal("After", updated.Name);

            var reloaded = await svc.GetByIdAsync(id);
            Assert.NotNull(reloaded);
            Assert.Equal("After", reloaded!.Name);
            Assert.Equal("You are v2.", reloaded.Config.Instruction);
            Assert.Contains(reloaded.ToolsConfiguration?.Tools ?? [], t => t.Function?.Name == "web_search");

            // delete+recreate left exactly one agent — no orphan/duplicate
            Assert.Single(await svc.GetAllAsync());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
