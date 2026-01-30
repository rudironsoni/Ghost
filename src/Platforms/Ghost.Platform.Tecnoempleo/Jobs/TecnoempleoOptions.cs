namespace Ghost.Platform.Tecnoempleo.Jobs;

public class TecnoempleoOptions
{
    public string BaseUrl { get; set; } = "https://www.tecnoempleo.com";
    public string ApiUrl { get; set; } = "https://api.tecnoempleo.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool EnableRateLimiting { get; set; } = true;
    public int MaxRequestsPerMinute { get; set; } = 30;
    public int MaxRequestsPerHour { get; set; } = 1000;
}