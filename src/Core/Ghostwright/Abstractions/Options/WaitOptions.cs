namespace Ghostwright;

public sealed class WaitOptions
{
    public int Timeout { get; set; } = 30_000;
    public WaitState State { get; set; } = WaitState.Load;
}

public enum WaitState
{
    Attached,
    Detached,
    Visible,
    Hidden,
    Load
}
