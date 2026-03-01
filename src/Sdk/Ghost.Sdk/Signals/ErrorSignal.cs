namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when an error occurs during spider execution.
/// </summary>
public sealed record ErrorSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the error occurred.</param>
    /// <param name="message">The error message.</param>
    /// <param name="exceptionType">The type of the exception.</param>
    /// <param name="stackTrace">Optional stack trace.</param>
    public ErrorSignal(string spiderId, DateTimeOffset timestamp, string message, string exceptionType, string? stackTrace = null)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exceptionType);
        SpiderId = spiderId;
        Timestamp = timestamp;
        Message = message;
        ExceptionType = exceptionType;
        StackTrace = stackTrace;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the type of the exception.
    /// </summary>
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the optional stack trace.
    /// </summary>
    public string? StackTrace { get; }
}
