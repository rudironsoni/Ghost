namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Contract that validates the spider has not exceeded a maximum number of requests.
/// </summary>
public class MaxRequestsContract : ISpiderContract
{
    /// <inheritdoc />
    public string Name => "MaxRequests";

    /// <summary>
    /// Gets or sets the maximum number of requests allowed.
    /// </summary>
    /// <remarks>
    /// Default value is 1000 requests.
    /// </remarks>
    public int MaxRequests { get; set; } = 1000;

    /// <inheritdoc />
    public Task<bool> ValidateAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.RequestCount < MaxRequests);
    }
}
