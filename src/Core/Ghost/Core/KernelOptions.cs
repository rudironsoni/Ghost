namespace Ghost.Core;

public sealed class KernelOptions
{
    public bool Headless { get; set; } = true;
    public int SlowMo { get; set; }
    public string? ProxyServer { get; set; }
    public int MaxConcurrentSessions { get; set; } = 10;
    
    /// <summary>
    /// Enables advanced stealth evasions to prevent bot detection.
    /// Defaults to true.
    /// </summary>
    public bool EnableStealth { get; set; } = true;

    /// <summary>
    /// Custom arguments to pass to the browser instance.
    /// </summary>
    public string[]? Args { get; set; }
    
    /// <summary>
    /// If true, the default stealth arguments (like --disable-blink-features) are NOT added.
    /// Useful for debugging.
    /// </summary>
    public bool DisableDefaultStealthArgs { get; set; }
}
