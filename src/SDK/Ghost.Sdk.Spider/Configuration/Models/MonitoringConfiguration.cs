namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for monitoring and observability.
/// </summary>
public sealed class MonitoringConfiguration
{
    /// <summary>
    /// Gets or sets whether monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to collect metrics.
    /// </summary>
    public bool CollectMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to emit diagnostic events.
    /// </summary>
    public bool EmitDiagnostics { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics export interval (seconds).
    /// </summary>
    public int MetricsExportIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets logging configuration.
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Gets or sets telemetry configuration.
    /// </summary>
    public TelemetryConfiguration Telemetry { get; set; } = new();

    /// <summary>
    /// Gets or sets health check configuration.
    /// </summary>
    public HealthCheckConfiguration HealthCheck { get; set; } = new();

    /// <summary>
    /// Gets or sets alert configuration.
    /// </summary>
    public AlertConfiguration Alerts { get; set; } = new();
}

/// <summary>
/// Configuration for logging.
/// </summary>
public sealed class LoggingConfiguration
{
    /// <summary>
    /// Gets or sets the minimum log level (Trace, Debug, Information, Warning, Error, Critical).
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets whether to log successful extractions.
    /// </summary>
    public bool LogSuccessfulExtractions { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to log failed extractions.
    /// </summary>
    public bool LogFailedExtractions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include extracted data in logs.
    /// </summary>
    public bool IncludeExtractedData { get; set; } = false;

    /// <summary>
    /// Gets or sets custom log enrichers.
    /// </summary>
    public List<string> Enrichers { get; set; } = new();
}

/// <summary>
/// Configuration for telemetry.
/// </summary>
public sealed class TelemetryConfiguration
{
    /// <summary>
    /// Gets or sets whether to export traces.
    /// </summary>
    public bool ExportTraces { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to export metrics.
    /// </summary>
    public bool ExportMetrics { get; set; } = false;

    /// <summary>
    /// Gets or sets the OTLP endpoint for telemetry export.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets custom attributes to add to all telemetry.
    /// </summary>
    public Dictionary<string, string> CustomAttributes { get; set; } = new();
}

/// <summary>
/// Configuration for health checks.
/// </summary>
public sealed class HealthCheckConfiguration
{
    /// <summary>
    /// Gets or sets whether health checks are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval (seconds).
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the health check timeout (seconds).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets custom health check endpoints.
    /// </summary>
    public List<string> CustomChecks { get; set; } = new();
}

/// <summary>
/// Configuration for alerts.
/// </summary>
public sealed class AlertConfiguration
{
    /// <summary>
    /// Gets or sets whether alerts are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets alert rules.
    /// </summary>
    public List<AlertRuleConfiguration> Rules { get; set; } = new();
}

/// <summary>
/// Configuration for an alert rule.
/// </summary>
public sealed class AlertRuleConfiguration
{
    /// <summary>
    /// Gets or sets the rule name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the condition expression.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alert severity (Info, Warning, Error, Critical).
    /// </summary>
    public string Severity { get; set; } = "Warning";

    /// <summary>
    /// Gets or sets the notification channels.
    /// </summary>
    public List<string> Channels { get; set; } = new();

    /// <summary>
    /// Gets or sets alert metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
