namespace Ghost.Engine.Engine;

/// <summary>
/// Configuration options for the Ghost engine.
/// </summary>
public sealed class GhostEngineOptions
{
    /// <summary>
    /// Maximum number of in-flight requests at any time.
    /// </summary>
    public int MaxInFlight { get; set; } = 10;

    /// <summary>
    /// Maximum number of pending items in the pipeline.
    /// </summary>
    public int MaxPendingItems { get; set; } = 100;

    /// <summary>
    /// TimeProvider for time-based operations. Defaults to TimeProvider.System.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
