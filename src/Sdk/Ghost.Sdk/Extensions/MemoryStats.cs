namespace Ghost.Sdk.Extensions;

/// <summary>
/// Represents memory usage statistics for a spider execution.
/// </summary>
/// <remarks>
/// This class provides detailed memory metrics including current usage, peak usage,
/// and percentage relative to configured limits. All measurements are in bytes.
/// </remarks>
public class MemoryStats
{
    /// <summary>
    /// Gets or sets the current memory usage in bytes.
    /// </summary>
    /// <value>The current memory consumption of the process.</value>
    public long CurrentBytes { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes since monitoring began.
    /// </summary>
    /// <value>The highest memory consumption observed during execution.</value>
    public long PeakBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed memory in bytes.
    /// </summary>
    /// <value>The configured memory limit, or 0 if unlimited.</value>
    public long MaxAllowedBytes { get; set; }

    /// <summary>
    /// Gets the current memory usage as a percentage of the maximum allowed.
    /// </summary>
    /// <value>
    /// A percentage value (0-100+) representing current usage relative to the limit.
    /// Returns 0 if <see cref="MaxAllowedBytes"/> is 0.
    /// </value>
    /// <remarks>
    /// Values over 100% indicate the current usage exceeds the configured limit.
    /// This can occur if the limit check hasn't triggered shutdown yet.
    /// </remarks>
    public double UsagePercent => MaxAllowedBytes > 0 ? (CurrentBytes / (double)MaxAllowedBytes) * 100 : 0;
}
