using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Interface for monitoring and limiting memory usage during spider execution.
/// </summary>
/// <remarks>
/// This extension provides real-time memory monitoring, configurable limits,
/// and detailed statistics about memory consumption. Implementations use this
/// to prevent out-of-memory errors in long-running crawls.
/// </remarks>
public interface IMemoryUsageExtension
{
    /// <summary>
    /// Gets or sets the maximum allowed memory usage in bytes.
    /// </summary>
    /// <value>The memory limit in bytes, or 0 for no limit.</value>
    /// <remarks>
    /// When current memory usage exceeds this value, <see cref="CheckMemoryAsync"/>
    /// will return <c>false</c> and signal the spider to stop.
    /// </remarks>
    public long MaxMemoryBytes { get; set; }

    /// <summary>
    /// Checks if current memory usage is within acceptable limits.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if memory usage is within limits; <c>false</c> if limits are exceeded
    /// and the spider should stop.
    /// </returns>
    /// <remarks>
    /// This method should be called periodically during spider execution to monitor
    /// memory consumption. It may trigger warnings or garbage collection based on
    /// configured thresholds.
    /// </remarks>
    public Task<bool> CheckMemoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets current memory usage statistics.
    /// </summary>
    /// <returns>A snapshot of current, peak, and maximum allowed memory usage.</returns>
    /// <remarks>
    /// The returned statistics include current memory consumption, peak usage since
    /// monitoring began, and the configured limit. Useful for logging and diagnostics.
    /// </remarks>
    public MemoryStats GetStats();
}
