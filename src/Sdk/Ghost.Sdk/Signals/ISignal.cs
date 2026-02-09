namespace Ghost.Sdk.Signals;

/// <summary>
/// Base interface for all signals in the spider event system.
/// </summary>
public interface ISignal
{
    /// <summary>
    /// Gets the timestamp when the signal was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the unique identifier of the spider that emitted this signal.
    /// </summary>
    string SpiderId { get; }
}
