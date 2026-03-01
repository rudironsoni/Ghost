namespace Ghost.Sdk.Extensions;

/// <summary>
/// Configuration options for memory usage monitoring during spider execution.
/// </summary>
/// <remarks>
/// These options control memory limits, warning thresholds, and garbage collection behavior.
/// All memory sizes are specified in bytes.
/// </remarks>
public class MemoryOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed memory usage in bytes.
    /// </summary>
    /// <value>
    /// The memory limit in bytes. Defaults to 512 MB (536,870,912 bytes).
    /// </value>
    /// <remarks>
    /// When memory usage exceeds this limit, the spider will be signaled to stop.
    /// Set to 0 for no limit (not recommended for production).
    /// </remarks>
    public long MaxMemoryBytes { get; set; } = 512 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the warning threshold as a percentage of <see cref="MaxMemoryBytes"/>.
    /// </summary>
    /// <value>
    /// A percentage value (0-100). Defaults to 80%.
    /// </value>
    /// <remarks>
    /// When memory usage crosses this threshold, a warning will be logged.
    /// This provides early visibility into memory pressure before limits are hit.
    /// </remarks>
    public double WarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets whether to enable aggressive garbage collection when approaching limits.
    /// </summary>
    /// <value>
    /// <c>true</c> to trigger garbage collection near memory limits; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the extension will call <see cref="System.GC.Collect()"/> when memory
    /// usage exceeds the warning threshold. This may improve memory reclamation at the cost
    /// of increased CPU usage and potential pauses.
    /// </remarks>
    public bool EnableGarbageCollection { get; set; } = true;
}
