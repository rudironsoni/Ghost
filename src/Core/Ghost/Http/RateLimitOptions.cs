namespace Ghost.Http;

public sealed record RateLimitOptions
{
    public int DelayMinMs { get; init; } = 250;
    public int DelayMaxMs { get; init; } = 1500;
    public int MaxRetries { get; init; } = 3;
    public double BackoffFactor { get; init; } = 2.0;
}
