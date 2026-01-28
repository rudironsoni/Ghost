namespace Ghost;

public sealed class ScreenshotOptions
{
    public string? Path { get; set; }
    public string Type { get; set; } = "png"; // png or jpeg
    public int? Quality { get; set; }
    public bool FullPage { get; set; }
}
