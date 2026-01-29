using Ghost.Models;

namespace Ghost.Platform.Indeed;

public class IndeedOptions
{
    public bool Enabled { get; set; } = true;
    public CountryCode Country { get; set; } = CountryCode.US;
    public int DelayMinMs { get; set; } = 500;
    public int DelayMaxMs { get; set; } = 1500;
    public int MaxRetries { get; set; } = 3;
    public string ApiKey { get; set; } = string.Empty;
}
