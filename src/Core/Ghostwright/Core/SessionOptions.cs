namespace Ghostwright.Core;

public sealed class SessionOptions
{
    public int ViewportWidth { get; set; } = 1280;
    public int ViewportHeight { get; set; } = 720;
    public string? UserAgent { get; set; }
}
