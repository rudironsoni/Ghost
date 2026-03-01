using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Close condition that triggers when the process memory usage exceeds a threshold.
/// </summary>
/// <remarks>
/// This condition is useful for preventing out-of-memory errors in long-running spiders
/// or when processing large datasets. Evaluates the current process working set memory.
/// </remarks>
public sealed class MemoryThresholdCondition : ICloseCondition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryThresholdCondition"/> class.
    /// </summary>
    /// <param name="maxMemoryBytes">The maximum memory usage in bytes before closing.</param>
    /// <exception cref="ArgumentException">Thrown when maxMemoryBytes is less than or equal to zero.</exception>
    public MemoryThresholdCondition(long maxMemoryBytes)
    {
        if (maxMemoryBytes <= 0)
        {
            throw new ArgumentException("MaxMemoryBytes must be greater than zero", nameof(maxMemoryBytes));
        }

        MaxMemoryBytes = maxMemoryBytes;
    }

    /// <inheritdoc/>
    public string Name => $"MemoryThreshold({MaxMemoryBytes / (1024.0 * 1024.0):F2} MB)";

    /// <summary>
    /// Determines whether the memory threshold has been exceeded.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the current process memory usage is greater than or equal to the threshold; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses <see cref="System.Diagnostics.Process.WorkingSet64"/> to measure memory,
    /// which includes both private and shared memory pages currently in physical RAM.
    /// </remarks>
    public Task<bool> IsMetAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        long currentMemory = process.WorkingSet64;

        return Task.FromResult(currentMemory >= MaxMemoryBytes);
    }

    /// <summary>
    /// Gets the configured maximum memory threshold in bytes.
    /// </summary>
    /// <value>The maximum memory usage in bytes before the condition is met.</value>
    public long MaxMemoryBytes { get; }

    /// <summary>
    /// Gets the configured maximum memory threshold in megabytes.
    /// </summary>
    /// <value>The maximum memory usage in MB before the condition is met.</value>
    public double MaxMemoryMB => MaxMemoryBytes / (1024.0 * 1024.0);
}
