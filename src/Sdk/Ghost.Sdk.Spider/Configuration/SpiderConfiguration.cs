using Ghost.Sdk.Spider.Configuration.Models;

namespace Ghost.Sdk.Spider.Configuration;

/// <summary>
/// Root configuration for a spider instance.
/// </summary>
public sealed class SpiderConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for this spider configuration.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name of the spider.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of this configuration.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the description of what this spider does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets custom tags for categorization and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the target URLs and patterns for crawling.
    /// </summary>
    public TargetConfiguration Target { get; set; } = new();

    /// <summary>
    /// Gets or sets the data extraction configuration.
    /// </summary>
    public ExtractionConfiguration? Extraction { get; set; }

    /// <summary>
    /// Gets or sets the navigation and crawling configuration.
    /// </summary>
    public NavigationConfiguration Navigation { get; set; } = new();

    /// <summary>
    /// Gets or sets the crawling strategies configuration.
    /// </summary>
    public StrategiesConfiguration Strategies { get; set; } = new();

    /// <summary>
    /// Gets or sets the data processing pipeline configuration.
    /// </summary>
    public PipelineConfiguration Pipeline { get; set; } = new();

    /// <summary>
    /// Gets or sets the storage backend configuration.
    /// </summary>
    public StorageConfiguration Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets the scheduling configuration for recurring crawls.
    /// </summary>
    public ScheduleConfiguration? Schedule { get; set; }

    /// <summary>
    /// Gets or sets the monitoring and observability configuration.
    /// </summary>
    public MonitoringConfiguration Monitoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the resource limits and constraints.
    /// </summary>
    public LimitsConfiguration Limits { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metadata for the spider.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
