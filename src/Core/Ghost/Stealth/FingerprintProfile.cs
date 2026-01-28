namespace Ghost.Stealth;

public sealed class FingerprintProfile
{
    public string Name { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public int ViewportWidth { get; init; }
    public int ViewportHeight { get; init; }

    public static FingerprintProfile DesktopDefault => new() { Name = "desktop-default", UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)", ViewportWidth = 1280, ViewportHeight = 720 };
}
