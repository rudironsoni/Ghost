namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for data processing pipeline.
/// </summary>
public sealed class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets whether the pipeline is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets pipeline stages to execute.
    /// </summary>
    public List<PipelineStageConfiguration> Stages { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to stop pipeline on stage failure.
    /// </summary>
    public bool StopOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the error handling strategy for pipeline failures.
    /// </summary>
    public string ErrorHandling { get; set; } = "Log";
}

/// <summary>
/// Configuration for a pipeline stage.
/// </summary>
public sealed class PipelineStageConfiguration
{
    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stage type (Validation, Transformation, Enrichment, Filter, Custom).
    /// </summary>
    public string Type { get; set; } = "Custom";

    /// <summary>
    /// Gets or sets whether this stage is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the execution order (lower values execute first).
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Gets or sets stage-specific configuration.
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Gets or sets the custom processor type name (for Custom type).
    /// </summary>
    public string? ProcessorType { get; set; }
}
