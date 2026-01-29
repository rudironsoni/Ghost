namespace Ghost.Platform.Google.Gemini;

/// <summary>
/// Options for the Gemini browser integration (moved from GoogleOptions).
/// </summary>
public sealed class GeminiOptions
{
    public string BaseUrl { get; set; } = "https://gemini.google.com";
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public string DefaultModel { get; set; } = "gemini-pro";
    public bool Enabled { get; set; } = true;
}
