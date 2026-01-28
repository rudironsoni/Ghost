namespace Ghost.Core;

public sealed class KernelOptions
{
    public bool Headless { get; set; } = true;
    public int SlowMo { get; set; }
    public string? ProxyServer { get; set; }
    public int MaxConcurrentSessions { get; set; } = 10;
}
