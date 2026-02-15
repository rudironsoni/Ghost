using System.Text.Json;
using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Engine;

/// <summary>
/// Executes a job by interpreting a SpiderSpec declarative configuration.
/// </summary>
public interface IDeclarativeEngine
{
    /// <summary>
    /// Runs the engine for a given job and specification.
    /// </summary>
    /// <param name="job">The job definition containing budgets and trace context.</param>
    /// <param name="spec">The spider specification defining the step graph.</param>
    /// <param name="observer">Observer for engine events and emitted items.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RunAsync(
        JobDefinition job,
        SpiderSpec spec,
        IEngineObserver observer,
        CancellationToken ct);
}

/// <summary>
/// Observer interface for receiving engine events and items.
/// </summary>
public interface IEngineObserver
{
    /// <summary>
    /// Called when an engine event occurs.
    /// </summary>
    ValueTask OnEventAsync(EngineEvent e, CancellationToken ct);
    
    /// <summary>
    /// Called when an item is emitted.
    /// </summary>
    ValueTask OnItemAsync(string itemType, JsonDocument item, CancellationToken ct);
}
