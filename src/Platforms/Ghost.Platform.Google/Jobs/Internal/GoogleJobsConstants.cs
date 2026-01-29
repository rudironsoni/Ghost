namespace Ghost.Platform.Google.Jobs.Internal;

internal static class GoogleJobsConstants
{
    public static readonly string[] DefaultHeaders = new[] {
        "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
    };

    // Async param key used in Google Jobs async calls (observed in JobSpy analysis)
    public const string AsyncParam = "_fmt";

    // Widget key for Google Jobs
    public const string WidgetKey = "520084652";

    // Regex to extract data-async-fc cursor attribute
    public const string DataAsyncFcRegex = "data-async-fc=\"(?<cursor>[^\"]+)\"";
}
