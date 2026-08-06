using MaIN.Core;
using MaIN.Core.Hub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MaIN.InferPage.IntegrationTests;

// AIHub, ModelRegistry, etc. are static, process-wide state. Every test class in this project
// that touches them joins the "InferPageEndpointTests" collection so xUnit runs them
// sequentially instead of in parallel across collections (see other test files for the same
// convention).
[Collection("InferPageEndpointTests")]
public class AgentPersistenceTests
{
    [Fact]
    public async Task Agent_created_with_filesystem_backend_is_readable_after_reinitialization()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "main-agent-persist-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaIN:FileSystemSettings:Path"] = tempDir
            })
            .Build();

        try
        {
            var services1 = new ServiceCollection();
            services1.AddLogging();
            services1.AddMaIN(config);
            await using var sp1 = services1.BuildServiceProvider();
            sp1.UseMaIN();

            var created = await AIHub.Agent()
                .WithModel("gpt-4o")
                .WithName("PersistMe")
                .WithInitialPrompt("You are a test agent.")
                .CreateAsync();
            var agentId = created.GetAgentId();

            var services2 = new ServiceCollection();
            services2.AddLogging();
            services2.AddMaIN(config);
            await using var sp2 = services2.BuildServiceProvider();
            sp2.UseMaIN();

            var reloaded = await AIHub.Agent().GetAgentById(agentId);

            Assert.NotNull(reloaded);
            Assert.Equal("PersistMe", reloaded!.Name);
            Assert.True(File.Exists(Path.Combine(tempDir, "agents", agentId + ".json")));
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
