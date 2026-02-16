namespace Ghost.Plugin.InfoJobs;

public sealed class InfoJobsOptions
{
    public bool Enabled { get; set; } = true;
    public string Country { get; set; } = "ES";
    public string Language { get; set; } = "es";
    public int MinDelayMs { get; set; } = 500;
    public int MaxDelayMs { get; set; } = 1500;

    /// <summary>
    /// InfoJobs API Client ID for authentication
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// InfoJobs API Client Secret for authentication
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// API endpoint for InfoJobs job searches
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://api.infojobs.net/api/";

    /// <summary>
    /// Base URL for InfoJobs website
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.infojobs.net/";
}
