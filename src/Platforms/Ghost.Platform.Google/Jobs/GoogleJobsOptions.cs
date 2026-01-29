namespace Ghost.Platform.Google.Jobs;

public sealed class GoogleJobsOptions
{
    public bool Enabled { get; set; } = true;
    public string Country { get; set; } = "US";
    public int MinDelayMs { get; set; } = 200;
    public int MaxDelayMs { get; set; } = 800;
}
