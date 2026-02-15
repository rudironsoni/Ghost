namespace Ghost.Smoke.Tests;

/// <summary>
/// Configuration for smoke tests targeting production endpoints.
/// </summary>
public static class SmokeTestConfiguration
{
    /// <summary>
    /// Gets the base URL for the Ghost production API.
    /// </summary>
    public static string BaseUrl => Environment.GetEnvironmentVariable("GHOST_BASE_URL") ?? "https://localhost:5001";

    /// <summary>
    /// Gets the API key for authentication (if required).
    /// </summary>
    public static string? ApiKey => Environment.GetEnvironmentVariable("GHOST_API_KEY");

    /// <summary>
    /// Gets the timeout for HTTP requests.
    /// </summary>
    public static TimeSpan RequestTimeout => TimeSpan.FromSeconds(30);
}
