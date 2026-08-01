namespace MaIN.InferPage.Services;

/// <summary>Event bus for NavBar ↔ Home sibling communication (agents overlay).</summary>
public class AgentsPanelStateService
{
    public event Action? OnRequested;

    public void Request()
    {
        OnRequested?.Invoke();
    }
}
