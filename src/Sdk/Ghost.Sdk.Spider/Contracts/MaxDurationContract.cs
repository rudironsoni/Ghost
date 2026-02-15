namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Contract that validates the spider has not exceeded a maximum execution duration.
/// </summary>
public class MaxDurationContract : ISpiderContract
{
    /// <inheritdoc />
    public string Name => "MaxDuration";

    /// <summary>
    /// Gets or sets the maximum duration allowed for spider execution.
    /// </summary>
    /// <remarks>
    /// Default value is 1 hour.
    /// </remarks>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public Task<bool> ValidateAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.Duration < MaxDuration);
    }
}
