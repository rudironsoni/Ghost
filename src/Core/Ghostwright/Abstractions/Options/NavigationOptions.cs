namespace Ghostwright;

public sealed class NavigationOptions
{
    public int Timeout { get; set; } = 30_000;
    public WaitUntil WaitUntil { get; set; } = WaitUntil.Load;
}

public enum WaitUntil
{
    Load,
    DomContentLoaded,
    NetworkIdle
}
