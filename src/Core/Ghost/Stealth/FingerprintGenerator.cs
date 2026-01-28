using System;
using System.Globalization;
using Ghost.Internal; // Assuming RandomUtil or similar exists, otherwise we use System.Random directly

namespace Ghost.Stealth;

public static class FingerprintGenerator
{
    /// <summary>
    /// Generates a new <see cref="FingerprintProfile"/> with randomized or default values.
    /// </summary>
    /// <returns>A new <see cref="FingerprintProfile"/> instance.</returns>
    public static FingerprintProfile Generate()
    {
        // stable random seed per context
        int seed = Random.Shared.Next(int.MinValue, int.MaxValue);
        var rnd = new Random(seed);

        int[] cores = [4, 6, 8, 12, 16, 24];
        int[] memories = [8, 16, 32];
        (int w, int h)[] screens = [(1920, 1080), (2560, 1440), (3840, 2160)];

        // Simple platform logic for now - stick to Windows/Chrome for highest stealth success rate
        string platform = "Win32";
        string chromeVersion = "120.0.0.0";
        string ua = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36";

        /* NYC bounding box: 40.55 – 40.96 N,  -74.25 – -73.70 W */
        double lat = 40.55 + rnd.NextDouble() * (40.96 - 40.55);
        double lng = -74.25 + rnd.NextDouble() * (-73.70 + 74.25);
        string timeZone = "America/New_York";

        // GPU spoofing - match high-end desktop traits
        string vendor = "Google Inc. (NVIDIA)";
        string renderer = "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)";

        return new FingerprintProfile
        {
            Name = "generated-" + seed,
            UserAgent = ua,
            ViewportWidth = screens[rnd.Next(screens.Length)].w,
            ViewportHeight = screens[rnd.Next(screens.Length)].h,
            Cores = cores[rnd.Next(cores.Length)],
            MemoryGb = memories[rnd.Next(memories.Length)],
            Platform = platform,
            ChromeVersion = chromeVersion,
            Seed = seed,
            Latitude = Math.Round(lat, 5),
            Longitude = Math.Round(lng, 5),
            TimeZone = timeZone,
            VideoCardVendor = vendor,
            VideoCardRenderer = renderer,
            BatteryLevel = 0.70 + (rnd.NextDouble() * 0.30), // 70-100%
            Rtt = 20 + rnd.NextDouble() * 80,
            Downlink = 5 + rnd.NextDouble() * 45
        };
    }
}
