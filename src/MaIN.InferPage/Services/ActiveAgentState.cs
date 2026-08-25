using MaIN.Domain.Entities.Agents;

namespace MaIN.InferPage.Services;

/// <summary>Holds the currently active agent for the main chat; shared between NavBar and Home.</summary>
public class ActiveAgentState
{
    public Agent? Current { get; private set; }

    public event Action? OnChanged;

    public void Set(Agent agent)
    {
        Current = agent;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        Current = null;
        OnChanged?.Invoke();
    }
}
