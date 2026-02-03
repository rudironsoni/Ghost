using Ghost.Contracts.Social;

namespace Ghost.Contracts.Simulation;

/// <summary>
/// Represents a recorded simulation action with full context and result.
/// </summary>
public class SimulationRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for this record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the timestamp when the simulation was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the target platform name.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action being simulated.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request context (e.g., CreatePostRequest).
    /// </summary>
    public object? RequestContext { get; set; }

    /// <summary>
    /// Gets or sets the simulation result.
    /// </summary>
    public SimulationResult Result { get; set; } = null!;

    /// <summary>
    /// Gets or sets the session identifier for grouping related actions.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for tracing across operations.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the duration of the simulation operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who initiated the simulation.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets additional context data for this record.
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Gets or sets the sequence number within the session.
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the parent record ID for related operations (e.g., thread replies).
    /// </summary>
    public Guid? ParentRecordId { get; set; }

    /// <summary>
    /// Gets or sets the list of child record IDs.
    /// </summary>
    public List<Guid> ChildRecordIds { get; set; } = new();
}
