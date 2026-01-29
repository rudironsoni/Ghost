namespace Ghost.Platform.Google;

/// <summary>
/// Options for the Gemini browser integration.
/// </summary>
public sealed class GoogleOptions
{
    // Initialize sub-options with sensible defaults so tests and consumers
    // don't need to manually new them up.
    public Gemini.GeminiOptions Gemini { get; set; } = new Gemini.GeminiOptions();
    public Jobs.GoogleJobsOptions Jobs { get; set; } = new Jobs.GoogleJobsOptions();
}
