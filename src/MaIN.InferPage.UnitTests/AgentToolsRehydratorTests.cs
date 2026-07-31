using System.Threading.Tasks;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Tools;
using MaIN.InferPage.Services;

namespace MaIN.InferPage.UnitTests;

public class AgentToolsRehydratorTests
{
    private static Chat ChatWithToolNamed(string toolName) => new()
    {
        Id = "c1",
        Name = "c1",
        ModelId = "m",
        Messages = [],
        ToolsConfiguration = new ToolsConfiguration
        {
            Tools =
            [
                new ToolDefinition
                {
                    Type = "function",
                    Function = new FunctionDefinition { Name = toolName, Parameters = new { } },
                    Execute = null
                }
            ]
        }
    };

    [Fact]
    public void Rehydrate_attaches_executor_for_a_known_built_in_tool()
    {
        var chat = ChatWithToolNamed("get_current_datetime");
        AgentToolsRehydrator.Rehydrate(chat, httpClientFactory: null, searxngBaseUrl: null);
        Assert.NotNull(chat.ToolsConfiguration!.Tools[0].Execute);
    }

    [Fact]
    public void Rehydrate_leaves_unknown_tools_untouched()
    {
        var chat = ChatWithToolNamed("some_custom_client_tool");
        AgentToolsRehydrator.Rehydrate(chat, httpClientFactory: null, searxngBaseUrl: null);
        Assert.Null(chat.ToolsConfiguration!.Tools[0].Execute);
    }

    [Fact]
    public void Rehydrate_is_a_noop_when_no_tools_configured()
    {
        var chat = new Chat { Id = "c", Name = "c", ModelId = "m", Messages = [] };
        AgentToolsRehydrator.Rehydrate(chat, httpClientFactory: null, searxngBaseUrl: null);
        Assert.Null(chat.ToolsConfiguration);
    }

    [Fact]
    public void Rehydrate_does_not_overwrite_an_existing_executor()
    {
        var chat = ChatWithToolNamed("get_current_datetime");
        Func<string, Task<string>> original = _ => Task.FromResult("original");
        chat.ToolsConfiguration!.Tools[0].Execute = original;

        AgentToolsRehydrator.Rehydrate(chat, httpClientFactory: null, searxngBaseUrl: null);

        Assert.Same(original, chat.ToolsConfiguration!.Tools[0].Execute);
    }
}
