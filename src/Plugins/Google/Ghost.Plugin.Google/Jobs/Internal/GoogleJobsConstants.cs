namespace Ghost.Plugin.Google.Jobs.Internal;

public static class GoogleJobsConstants
{
    // Cookie-based consent bypass constants
    // Set CONSENT to YES (no +1) to align with common bypass pattern
    public const string ConsentCookie = "CONSENT=YES";
    public const string SocsCookie = "SOCS=CAESE";
    public const string CookieDomain = ".google.com";
    public const string CookiePath = "/";

    // User agent rotation for better stealth
    private static readonly string[] s_userAgents = new[]
    {
        // Chrome on macOS
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        // Chrome on Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 11.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        // Firefox on macOS
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:133.0) Gecko/20100101 Firefox/133.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:132.0) Gecko/20100101 Firefox/132.0",
        // Firefox on Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
        // Safari on macOS
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.2 Safari/605.1.15",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
        // Edge on Windows
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36 Edg/133.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36 Edg/132.0.0.0",
    };

    private static readonly Random s_random = new Random();

    /// <summary>
    /// Gets a random user agent from the pool for rotation
    /// </summary>
    public static string GetRandomUserAgent()
    {
        return s_userAgents[s_random.Next(s_userAgents.Length)];
    }

    // Updated to align with JobSpy headers (more extensive sec-ch-ua values and Google-specific x-browser-* headers)
    public static readonly Dictionary<string, string> SearchHeaders = new()
    {
        // User-Agent updated to Chrome 133 for better compatibility
        ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Priority"] = "u=0, i",
        ["Referer"] = "https://www.google.com/",
        ["Sec-Ch-Prefers-Color-Scheme"] = "dark",
        ["Sec-Ch-Ua"] = "\"Chromium\";v=\"133\", \"Google Chrome\";v=\"133\", \"Not?A_Brand\";v=\"99\"",
        ["Sec-Ch-Ua-Arch"] = "\"arm\"",
        ["Sec-Ch-Ua-Bitness"] = "\"64\"",
        ["Sec-Ch-Ua-Form-Factors"] = "\"Desktop\"",
        ["Sec-Ch-Ua-Full-Version"] = "\"133.0.0.0\"",
        ["Sec-Ch-Ua-Full-Version-List"] = "\"Chromium\";v=\"133.0.0.0\", \"Google Chrome\";v=\"133.0.0.0\", \"Not?A_Brand\";v=\"99.0.0.0\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Model"] = "\"\"",
        ["Sec-Ch-Ua-Platform"] = "\"macOS\"",
        ["Sec-Ch-Ua-Platform-Version"] = "\"15.0.1\"",
        ["Sec-Ch-Ua-Wow64"] = "?0",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "same-origin",
        ["Sec-Fetch-User"] = "?1",
        ["Upgrade-Insecure-Requests"] = "1",
        ["X-Browser-Channel"] = "stable",
        ["X-Browser-Copyright"] = "Copyright 2024 Google LLC. All rights reserved.",
        ["X-Browser-Year"] = "2024",
        ["Accept-Encoding"] = "gzip, deflate, br"
    };

    // Cookies to inject for consent bypass (domain-scoped)
    public static readonly Dictionary<string, string> ConsentCookies = new()
    {
        ["CONSENT"] = "YES",
        ["SOCS"] = "CAESE"
    };

    // Async headers used when fetching job data (async endpoints)
    public static readonly Dictionary<string, string> AsyncHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        ["Accept"] = "*/*",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Referer"] = "https://www.google.com/",
        ["Priority"] = "u=0, i",
        ["Sec-Fetch-Dest"] = "empty",
        ["Sec-Fetch-Mode"] = "cors",
        ["Sec-Fetch-Site"] = "same-origin",
        ["Sec-Ch-Ua"] = "\"Chromium\";v=\"133\", \"Google Chrome\";v=\"133\", \"Not?A_Brand\";v=\"99\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"macOS\"",
    };

    // Async bootstrap string from JobSpy - critical for Google Jobs async calls
    // This is a long basejs/xjs bootstrap string that Google expects
    public const string AsyncBootstrapString = "_basejs:/xjs/_/js/k=xjs.s.en_US.JwveA-JiKmg.2018.O/am=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAAAAACAAAoICAAAAAAAKMAfAAAAIAQAAAAAAAAAAAAACCAAAEJDAAACAAAAAGABAIAAARBAAABAAAAAgAgQAABAASKAfv8JAAABAAAAAAwAQAQACQAAAAAAcAEAQABoCAAAABAAAIABAACAAAAEAAAAFAAAAAAAAAAAAAAAAAAAAAAAAACAQADoBwAAAAAAAAAAAAAQBAAAAATQAAoACOAHAAAAAAAAAQAAAIIAAAA_ZAACAAAAAAAAcB8APB4wHFJ4AAAAAAAAAAAAAAAACECCYA5If0EACAAAAAAAAAAAAAAAAAAAUgRNXG4AMAE/dg=0/br=1/rs=ACT90oGxMeaFMCopIHq5tuQM-6_3M_VMjQ,_basecss:/xjs/_/ss/k=xjs.s.IwsGu62EDtU.L.B1.O/am=QOoQIAQAAAQAREADEBAAAAAAAAAAAAAAAAAAAAAgAQAAIAAAgAQAAAIAIAIAoEwCAADIC8AfsgEAawwAPkAAjgoAGAAAAAAAAEADAAAAAAIgAECHAAAAAAAAAAABAQAggAARQAAAQCEAAAAAIAAAABgAAAAAIAQIACCAAfB-AAFIQABoCEA_CgEAAIABAACEgHAEwwAEFQAM4CgAAAAAAAAAAAAACABCAAAAQEAAABAgAMCPAAA4AoE2BAEAggSAAIoAQAAAAAgAAAAACCAQAAAxEwA_ZAACAAAAAAAAAAkAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAQAEAAAAAAAAAAAAAAAAAAAAAQA/br=1/rs=ACT90oGZc36t3uUQkj0srnIvvbHjO2hgyg,_basecomb:/xjs/_/js/k=xjs.s.en_US.JwveA-JiKmg.2018.O/ck=xjs.s.IwsGu62EDtU.L.B1.O/am=QOoQIAQAAAQAREADEBAAAAAAAAAAAAAAAAAAAAAgAQAAIAAAgAQAAAKAIAoIqEwCAADIK8AfsgEAawwAPkAAjgoAGAAACCAAAEJDAAACAAIgAGCHAIAAARBAAABBAQAggAgRQABAQSOAfv8JIAABABgAAAwAYAQICSCAAfB-cAFIQABoCEA_ChEAAIABAACEgHAEwwAEFQAM4CgAAAAAAAAAAAAACABCAACAQEDoBxAgAMCPAAA4AoE2BAEAggTQAIoASOAHAAgAAAAACSAQAIIxEwA_ZAACAAAAAAAAcB8APB4wHFJ4AAAAAAAAAAAAAAAACECCYA5If0EACAAAAAAAAAAAAAAAAAAAUgRNXG4AMAE/d=1/ed=1/dg=0/br=1/ujg=1/rs=ACT90oFNLTjPzD_OAqhhtXwe2pg1T3WpBg,_fmt:prog,_id:fc_5FwaZ86OKsfdwN4P4La3yA4_2";

    // Widget key for Google Jobs
    public const string WidgetKey = "520084652";

    // Regex to extract data-async-fc cursor attribute
    public const string DataAsyncFcRegex = "data-async-fc=\"(?<cursor>[^\"]+)\"";
}
