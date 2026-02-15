namespace Ghost.Kernel;

public sealed class SessionOptions
{
    public int ViewportWidth { get; set; } = 1280;
    public int ViewportHeight { get; set; } = 720;
    public string? UserAgent { get; set; }
    public ProxySettings? Proxy { get; set; }
    public GeolocationSettings? Geolocation { get; set; }
    public string? TimezoneId { get; set; }
    public string? Locale { get; set; }
    public List<string> Permissions { get; set; } = new();
    public string? StorageStatePath { get; set; }

    public record ProxySettings(string Server, string? Username = null, string? Password = null, string? Bypass = null);
    public record GeolocationSettings(double Latitude, double Longitude, double Accuracy = 0);
}
