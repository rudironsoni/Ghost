using Ghost.Models;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorOptions
{
    public bool Enabled { get; set; } = true;
    public CountryCode Country { get; set; } = CountryCode.US;
    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// </summary>
    public int DelayMinMs { get; set; } = 500;
}
