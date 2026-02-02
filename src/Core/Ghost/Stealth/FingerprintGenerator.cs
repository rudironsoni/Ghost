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
        int seed = Random.Shared.Next(int.MinValue, int.MaxValue);
        var rnd = new Random(seed);

        // Expanded hardware options for more diversity (JobSpy-style variations)
        int[] cores = [2, 4, 6, 8, 12, 16, 18, 20, 24, 32, 48];
        int[] memories = [4, 8, 16, 32, 64];
        int[] deviceMemories = [2, 4, 8, 16, 32]; // GB (navigator.deviceMemory)
        (int w, int h, int d, string p, string os)[] platforms = [
            // Windows variants
            (1920, 1080, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1920, 1080, 24, "Windows", "Windows NT 11.0; Win64; x64"),
            (2560, 1440, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (3840, 2160, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1366, 768, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (2560, 1600, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1536, 864, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (2880, 1800, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (5120, 2880, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (2736, 1824, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (3072, 1728, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1440, 900, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1920, 1200, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (3440, 1440, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            (1680, 1050, 24, "Windows", "Windows NT 10.0; Win64; x64"),
            // Macintosh variants (with different macOS versions)
            (1920, 1080, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_6_1"),
            (1920, 1080, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_2_1"),
            (2560, 1440, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_6_1"),
            (3840, 2160, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_6_1"),
            (1920, 1200, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_5_0"),
            (3440, 1440, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_4_0"),
            (2560, 1440, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_3_0"),
            (3072, 1920, 24, "Macintosh", "Macintosh; Intel Mac OS X 14_2_0"),
            (2304, 1440, 24, "Macintosh", "Macintosh; Intel Mac OS X 13_6_0"),
            (2560, 1600, 24, "Macintosh", "Macintosh; Intel Mac OS X 13_5_0"),
            // Linux variants (different distributions)
            (1920, 1080, 24, "Linux x86_64", "X11; Linux x86_64"),
            (2560, 1440, 24, "Linux x86_64", "X11; Linux x86_64"),
            (3840, 2160, 24, "Linux x86_64", "X11; Linux x86_64"),
            (1920, 1080, 24, "Linux x86_64", "X11; Ubuntu; Linux x86_64"),
            (2560, 1440, 24, "Linux x86_64", "X11; Ubuntu; Linux x86_64"),
            (3840, 2160, 24, "Linux x86_64", "X11; Ubuntu; Linux x86_64"),
        ];

        // Multi-language support for timezone (JobSpy pattern)
        (string tz, double minLat, double maxLat, double minLng, double maxLng)[] timezones = [
            ("America/New_York", 40.5, 41.0, -74.25, -73.7)
        ];

        // GPU vendor variations (JobSpy-style hardware diversity)
        (string vendor, string[] renderers)[] gpus = [
            ("Google Inc. (NVIDIA)", [
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Ti Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3070 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3070 Ti Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3080 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 3080 Ti Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 4070 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 4070 Ti Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 4080 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce RTX 4090 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce GTX 1650 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (NVIDIA, NVIDIA GeForce GTX 1660 Ti Direct3D11 vs_5_0 ps_5_0, D3D11)"
            ]),
            ("Google Inc. (AMD)", [
                "ANGLE (AMD, AMD Radeon RX 6600 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 6700 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 6800 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 6800 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 6900 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 7600 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 7800 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 7900 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 5700 XT Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (AMD, AMD Radeon RX 5500 XT Direct3D11 vs_5_0 ps_5_0, D3D11)"
            ]),
            ("Intel Inc.", [
                "ANGLE (Intel(R) Iris(R) Graphics 610 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) Iris(R) Xe Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) UHD Graphics 630 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) UHD Graphics 750 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) UHD Graphics 770 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) Arc A380 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) Arc A770 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) HD Graphics 630 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                "ANGLE (Intel(R) Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)"
            ])
        ];

        // Select random platform, timezone, and GPU combination
        (int w, int h, int d, string p, string os) selectedPlatform = platforms[rnd.Next(platforms.Length)];
        (string tz, double minLat, double maxLat, double minLng, double maxLng) selectedTz = timezones[rnd.Next(timezones.Length)];
        (string vendor, string[] renderers) selectedGpu = gpus[rnd.Next(gpus.Length)];

        // Generate random location within timezone bounds
        double lat = selectedTz.minLat + rnd.NextDouble() * (selectedTz.maxLat - selectedTz.minLat);
        double lng = selectedTz.minLng + rnd.NextDouble() * (selectedTz.maxLng - selectedTz.minLng);

        // Chrome version variations (expanded to mimic realistic user base)
        string[] chromeVersions = [
            "100.0.4896.127", "109.0.5414.120", "114.0.5735.133",
            "119.0.6045.123", "120.0.6099.216", "121.0.6167.85",
            "122.0.6261.94", "123.0.6312.86", "124.0.6367.60",
            "125.0.6422.112", "126.0.6478.54", "127.0.6533.72",
            "128.0.6613.84", "129.0.6668.58", "130.0.6723.91",
            "131.0.6778.85", "132.0.6834.82", "133.0.6943.93"
        ];
        string chromeVersion = chromeVersions[rnd.Next(chromeVersions.Length)];

        // Connection types (navigator.connection.effectiveType)
        string[] connectionTypes = ["4g", "wifi", "ethernet"];
        string connectionType = connectionTypes[rnd.Next(connectionTypes.Length)];

        // Screen orientation (landscape-primary, portrait-primary)
        string[] orientations = ["landscape-primary", "landscape-primary", "portrait-primary"];
        string orientation = orientations[rnd.Next(orientations.Length)];

        // User agent generation for selected platform
        string ua = selectedPlatform.os switch
        {
            string s when s.Contains("Windows NT") => $"Mozilla/5.0 ({selectedPlatform.os}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36",
            string s when s.Contains("Macintosh") => $"Mozilla/5.0 ({selectedPlatform.os}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36",
            string s when s.Contains("Linux") => $"Mozilla/5.0 ({selectedPlatform.os}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36",
            _ => $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVersion} Safari/537.36"
        };

        string selectedRenderer = selectedGpu.renderers[rnd.Next(selectedGpu.renderers.Length)];

        return new FingerprintProfile
        {
            Name = "generated-" + seed,
            UserAgent = ua,
            ViewportWidth = selectedPlatform.w,
            ViewportHeight = selectedPlatform.h,
            ScreenColorDepth = selectedPlatform.d,
            Cores = cores[rnd.Next(cores.Length)],
            MemoryGb = memories[rnd.Next(memories.Length)],
            DeviceMemoryGb = deviceMemories[rnd.Next(deviceMemories.Length)],
            Platform = selectedPlatform.p,
            OperatingSystem = selectedPlatform.os,
            ChromeVersion = chromeVersion,
            Seed = seed,
            Latitude = Math.Round(lat, 5),
            Longitude = Math.Round(lng, 5),
            TimeZone = selectedTz.tz,
            VideoCardVendor = selectedGpu.vendor,
            VideoCardRenderer = selectedRenderer,
            BatteryLevel = 0.70 + (rnd.NextDouble() * 0.30),
            Rtt = 15 + rnd.NextDouble() * 100,
            Downlink = 3 + rnd.Next(50),
            ConnectionType = connectionType,
            ScreenOrientation = orientation
        };
    }
}
