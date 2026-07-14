using System.Text.Json;
using MaIN.Domain.Entities.Tools;
using MaIN.Domain.Exceptions.Tools;

namespace MaIN.Core.Hub.Utils;

public sealed class ToolsConfigurationBuilder
{
    private static readonly JsonSerializerOptions s_deserializeOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ToolsConfiguration _config = new() { Tools = [] };

    public ToolsConfigurationBuilder AddDefaultTool(string type)
    {
        _config.Tools.Add(new ToolDefinition { Type = type });
        return this;
    }

    public ToolsConfigurationBuilder AddTool(ToolDefinition tool)
    {
        _config.Tools.Add(tool);
        return this;
    }

    public ToolsConfigurationBuilder AddTool(
        string name,
        string description,
        object parameters,
        Func<string, Task<string>> execute,
        bool isClientSide = false)
    {
        return AddToolCore(name, description, parameters, execute, isClientSide);
    }

    public ToolsConfigurationBuilder AddTool(
        string name,
        string description,
        object parameters,
        Func<string, string> execute)
    {
        return AddToolCore(name, description, parameters, args => Task.FromResult(execute(args)));
    }

    public ToolsConfigurationBuilder AddTool<TArgs>(
        string name,
        string description,
        object parameters,
        Func<TArgs, Task<object>> execute) where TArgs : class
    {
        return AddToolCore(name, description, parameters, async argsJson =>
            {
                var args = JsonSerializer.Deserialize<TArgs>(argsJson, s_deserializeOptions)!;
                return JsonSerializer.Serialize(await execute(args));
            });
    }

    public ToolsConfigurationBuilder AddTool<TArgs>(
        string name,
        string description,
        object parameters,
        Func<TArgs, object> execute) where TArgs : class
    {
        return AddToolCore(name, description, parameters, argsJson =>
            {
                var args = JsonSerializer.Deserialize<TArgs>(argsJson, s_deserializeOptions)!;
                return Task.FromResult(JsonSerializer.Serialize(execute(args)));
            });
    }

    public ToolsConfigurationBuilder AddTool(
        string name,
        string description,
        Func<Task<object>> execute)
    {
        return AddToolCore(
            name,
            description,
            new { type = "object", properties = new { } },
            async _ => JsonSerializer.Serialize(await execute()));
    }

    public ToolsConfigurationBuilder AddTool(
        string name,
        string description,
        Func<object> execute)
        => AddToolCore(
            name,
            description,
            new { type = "object", properties = new { } },
            _ => Task.FromResult(JsonSerializer.Serialize(execute())));

    private ToolsConfigurationBuilder AddToolCore(
        string name,
        string description,
        object parameters,
        Func<string, Task<string>> execute,
        bool isClientSide = false)
    {
        _config.Tools.Add(new ToolDefinition
        {
            Function = new FunctionDefinition { Name = name, Description = description, Parameters = parameters },
            Execute = execute,
            IsClientSide = isClientSide
        });
        return this;
    }

    public ToolsConfigurationBuilder WithToolChoice(string choice)
    {
        _config.ToolChoice = choice;
        return this;
    }

    public ToolsConfigurationBuilder WithMaxIterations(int maxIterations)
    {
        InvalidToolIterationsException.ThrowIfInvalid(maxIterations);
        _config.MaxIterations = maxIterations;
        return this;
    }

    public ToolsConfiguration Build() => _config;
}
