namespace Ghost;

public sealed class PageOptions
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public string? UserAgent { get; set; }
    public bool JavaScriptEnabled { get; set; } = true;
    public string? TimezoneId { get; set; }
    public string? Locale { get; set; }
}
