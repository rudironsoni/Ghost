namespace Ghost.Sdk.Signals;

/// <summary>
/// Base interface for all signals in the spider event system.
/// </summary>
public interface ISignal
{
    /// <summary>
    /// Gets the timestamp when the signal was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the unique identifier of the spider that emitted this signal.
    /// </summary>
    public string SpiderId { get; }
}
