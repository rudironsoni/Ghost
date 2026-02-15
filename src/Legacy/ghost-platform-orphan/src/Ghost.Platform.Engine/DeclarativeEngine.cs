#pragma warning disable CA1848 // Use LoggerMessage delegates
#pragma warning disable CA1873 // Avoid expensive logging evaluation

using System.Diagnostics;
using System.Text.Json;
using Ghost.Platform.Events;
using Ghost.Sdk.Contracts;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Engine;

/// <summary>
/// Implementation of the declarative engine that interprets SpiderSpec graphs.
/// </summary>
public sealed class DeclarativeEngine : IDeclarativeEngine
{
    private readonly ILogger<DeclarativeEngine> _logger;
    private readonly IRunEventStore _eventStore;

    public DeclarativeEngine(
        ILogger<DeclarativeEngine> logger,
        IRunEventStore eventStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    public async Task RunAsync(
        JobDefinition job,
        SpiderSpec spec,
        IEngineObserver observer,
        CancellationToken ct)
    {
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        jobCts.CancelAfter(job.Budgets.MaxDuration);

        var context = new ExecutionContext(
            job,
            spec,
            observer,
            jobCts.Token);

        try
        {
            await EmitEventAsync(context, CreateEvent(context, "RunStarted"));
            
            // Start with entry step
            if (spec.Steps.TryGetValue(spec.EntryStepId, out var entryStep))
            {
                await ExecuteStepAsync(context, entryStep);
            }
            else
            {
                throw new InvalidOperationException($"Entry step '{spec.EntryStepId}' not found in specification");
            }

            await EmitEventAsync(context, CreateEvent(context, "RunCompleted"));
        }
        catch (OperationCanceledException) when (jobCts.Token.IsCancellationRequested)
        {
            await EmitEventAsync(context, CreateEvent(context, "RunCancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job execution failed for {JobId}", job.JobId);
            await EmitEventAsync(context, CreateEvent(context, "RunFailed", ex.Message));
            throw;
        }
    }

    private async Task ExecuteStepAsync(ExecutionContext context, Ghost.Sdk.Contracts.StepSpec step)
    {
        var stepEvent = CreateEvent(context, "StepStarted", causationId: null, data: new Dictionary<string, object?>
        {
            ["stepId"] = step.StepId,
            ["stepKind"] = step.Kind
        });
        await EmitEventAsync(context, stepEvent);

        try
        {
            // Interpret step based on kind
            switch (step.Kind)
            {
                case "build_request":
                    await ExecuteBuildRequestStepAsync(context, step);
                    break;
                case "http_fetch":
                    await ExecuteHttpFetchStepAsync(context, step);
                    break;
                case "parse":
                    await ExecuteParseStepAsync(context, step);
                    break;
                case "emit_item":
                    await ExecuteEmitItemStepAsync(context, step);
                    break;
                default:
                    _logger.LogWarning("Unknown step kind: {StepKind}", step.Kind);
                    break;
            }

            await EmitEventAsync(context, CreateEvent(context, "StepCompleted", causationId: stepEvent.EventId, data: new Dictionary<string, object?>
            {
                ["stepId"] = step.StepId
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step execution failed: {StepId}", step.StepId);
            await EmitEventAsync(context, CreateEvent(context, "StepFailed", causationId: stepEvent.EventId, data: new Dictionary<string, object?>
            {
                ["stepId"] = step.StepId,
                ["error"] = ex.Message
            }));
            throw;
        }
    }

    private Task ExecuteBuildRequestStepAsync(ExecutionContext context, Ghost.Sdk.Contracts.StepSpec step)
    {
        // TODO: Implement request building logic
        _logger.LogDebug("Executing build_request step: {StepId}", step.StepId);
        return Task.CompletedTask;
    }

    private Task ExecuteHttpFetchStepAsync(ExecutionContext context, Ghost.Sdk.Contracts.StepSpec step)
    {
        // Check budget
        if (!context.TryIncrementRequest())
        {
            throw new InvalidOperationException("Request budget exceeded");
        }

        // TODO: Implement HTTP fetch logic
        _logger.LogDebug("Executing http_fetch step: {StepId}", step.StepId);
        return Task.CompletedTask;
    }

    private Task ExecuteParseStepAsync(ExecutionContext context, Ghost.Sdk.Contracts.StepSpec step)
    {
        // TODO: Implement parsing logic
        _logger.LogDebug("Executing parse step: {StepId}", step.StepId);
        return Task.CompletedTask;
    }

    private async Task ExecuteEmitItemStepAsync(ExecutionContext context, Ghost.Sdk.Contracts.StepSpec step)
    {
        // Check budget
        if (!context.TryIncrementItem())
        {
            throw new InvalidOperationException("Item budget exceeded");
        }

        // TODO: Extract item from context and emit
        _logger.LogDebug("Executing emit_item step: {StepId}", step.StepId);
        
        // Emit placeholder item for now
        var item = JsonSerializer.SerializeToDocument(new { placeholder = true });
        await context.Observer.OnItemAsync("default", item, context.CancellationToken);
    }

    private async Task EmitEventAsync(ExecutionContext context, EngineEvent e)
    {
        await _eventStore.AppendAsync(e, context.CancellationToken);
        await context.Observer.OnEventAsync(e, context.CancellationToken);
    }

    private static EngineEvent CreateEvent(
        ExecutionContext context,
        string kind,
        string? errorMessage = null,
        string? causationId = null,
        Dictionary<string, object?>? data = null)
    {
        var eventData = data ?? new Dictionary<string, object?>();
        if (errorMessage != null)
        {
            eventData["error"] = errorMessage;
        }

        return new EngineEvent(
            EventId: Guid.NewGuid().ToString("N"),
            SchemaVersion: 1,
            RunId: context.RunId,
            JobId: context.Job.JobId,
            Kind: kind,
            TimestampUtc: DateTimeOffset.UtcNow,
            CorrelationId: context.Job.TraceContext.CorrelationId,
            CausationId: causationId ?? context.Job.TraceContext.CausationId,
            TraceParent: context.Job.TraceContext.TraceParent,
            Baggage: context.Job.TraceContext.Baggage,
            Data: eventData);
    }
}
