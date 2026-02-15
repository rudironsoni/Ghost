namespace Ghost.Pool;

public sealed class TieredBrowserPoolOptions
{
    public HotPoolOptions Hot { get; set; } = new();
    public WarmPoolOptions Warm { get; set; } = new();
    public ColdPoolOptions Cold { get; set; } = new();
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public double MemoryPressureThreshold { get; set; } = 0.85;
}

public sealed class HotPoolOptions
{
    public int MinimumSize { get; set; } = 2;
    public int MaximumSize { get; set; } = 10;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class WarmPoolOptions
{
    public int MinimumSize { get; set; } = 5;
    public int MaximumSize { get; set; } = 20;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(10);
}

public sealed class ColdPoolOptions
{
    public int MaximumConcurrent { get; set; } = 50;
}
