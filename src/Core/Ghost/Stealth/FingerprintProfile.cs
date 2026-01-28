using System;

namespace Ghost.Stealth;

/// <summary>
/// Represents a coherent hardware and location profile for a Playwright context.
/// This record encapsulates various hardware and geographical attributes to mimic a real user's environment.
/// </summary>
public sealed record FingerprintProfile
{
    public required string Name { get; init; }
    public required string UserAgent { get; init; }
    public required int ViewportWidth { get; init; }
    public required int ViewportHeight { get; init; }
    public required int Cores { get; init; }
    public required int MemoryGb { get; init; }
    public required string Platform { get; init; }
    public required string ChromeVersion { get; init; }
    public required int Seed { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required string TimeZone { get; init; }
    public required string VideoCardVendor { get; init; }
    public required string VideoCardRenderer { get; init; }
    public required double BatteryLevel { get; init; }

    // Network characteristics
    public double Rtt { get; init; }
    public double Downlink { get; init; }

    public static FingerprintProfile DesktopDefault => new()
    {
        Name = "desktop-default",
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ViewportWidth = 1920,
        ViewportHeight = 1080,
        Cores = 8,
        MemoryGb = 16,
        Platform = "Win32",
        ChromeVersion = "120.0.0.0",
        Seed = 12345,
        Latitude = 40.7128,
        Longitude = -74.0060,
        TimeZone = "America/New_York",
        VideoCardVendor = "Google Inc. (NVIDIA)",
        VideoCardRenderer = "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)",
        BatteryLevel = 0.95,
        Rtt = 50,
        Downlink = 10
    };
}
