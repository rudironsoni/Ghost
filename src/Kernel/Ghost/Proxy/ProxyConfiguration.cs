namespace Ghost.Proxy;

public sealed class ProxyConfiguration
{
    public bool Enabled { get; set; } = true;
    public ProxySelectionStrategy Strategy { get; set; } = ProxySelectionStrategy.RoundRobin;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int HealthCheckRetries { get; set; } = 3;
    public bool EnableHealthChecks { get; set; } = true;
    public bool EnableGeographicRouting { get; set; } = true;
    public List<ProxyProviderConfig> Providers { get; set; } = [];
    public Dictionary<string, List<string>> CountryToProviderMapping { get; set; } = new();
}

public enum ProxySelectionStrategy
{
    RoundRobin,
    LeastUsed,
    Random,
    Geographic,
    Weighted
}

public sealed class ProxyProviderConfig
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public bool Enabled { get; set; } = true;
    public int Weight { get; set; } = 100;
    public List<string> SupportedCountries { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
}

public sealed class ProxyHealthStatus
{
    public required string ProviderName { get; set; }
    public required string Host { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime? LastFailure { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public string? LastErrorMessage { get; set; }
}
