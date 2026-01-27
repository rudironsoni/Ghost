namespace Ghostwright.Platform.Google;

/// <summary>
/// Options for the Gemini browser integration.
/// </summary>
public sealed class GoogleOptions
{
    public string BaseUrl { get; set; } = "https://gemini.google.com";
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public string DefaultModel { get; set; } = "gemini-pro";
}
