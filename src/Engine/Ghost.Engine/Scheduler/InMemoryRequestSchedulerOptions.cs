namespace Ghost.Engine.Scheduler;

/// <summary>
/// Options for configuring the in-memory request scheduler.
/// </summary>
public sealed class InMemoryRequestSchedulerOptions
{
    /// <summary>
    /// Optional deduplication function. If provided, returns true to skip duplicate requests.
    /// </summary>
    public Func<Ghost.Engine.Abstractions.Transport.GhostRequest, bool>? ShouldSkip { get; set; }
}
