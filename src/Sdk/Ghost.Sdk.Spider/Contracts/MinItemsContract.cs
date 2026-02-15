namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Contract that validates the spider has extracted a minimum number of items.
/// </summary>
public class MinItemsContract : ISpiderContract
{
    /// <inheritdoc />
    public string Name => "MinItems";

    /// <summary>
    /// Gets or sets the minimum number of items that must be extracted.
    /// </summary>
    /// <remarks>
    /// Default value is 1 item.
    /// </remarks>
    public int MinItems { get; set; } = 1;

    /// <inheritdoc />
    public Task<bool> ValidateAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.ItemCount >= MinItems);
    }
}
