namespace MaIN.InferPage.Endpoints;

public static class AgentModelRef
{
    public const string Prefix = "agent:";

    public static bool TryParse(string? model, out string agentId)
    {
        agentId = string.Empty;
        if (string.IsNullOrWhiteSpace(model) || !model.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var id = model[Prefix.Length..].Trim();
        if (id.Length == 0) return false;
        agentId = id;
        return true;
    }

    public static string Format(string agentId) => Prefix + agentId;
}
