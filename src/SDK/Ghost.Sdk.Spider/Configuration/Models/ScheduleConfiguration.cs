namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for spider scheduling.
/// </summary>
public sealed class ScheduleConfiguration
{
    /// <summary>
    /// Gets or sets whether scheduling is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the schedule type (Cron, Interval, Once).
    /// </summary>
    public string Type { get; set; } = "Cron";

    /// <summary>
    /// Gets or sets the cron expression (for Cron type).
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the interval in seconds (for Interval type).
    /// </summary>
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the specific time to run (for Once type).
    /// </summary>
    public DateTime? RunAt { get; set; }

    /// <summary>
    /// Gets or sets the timezone for the schedule.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets whether to run immediately on startup.
    /// </summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum runtime (seconds). 0 means no limit.
    /// </summary>
    public int MaxRuntimeSeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether concurrent executions are allowed.
    /// </summary>
    public bool AllowConcurrentExecutions { get; set; } = false;
}
