using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Resilience;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public class ResilientJobScraper : IJobScraper
{
    private static readonly Action<ILogger, string, CircuitState, Exception?> _logCircuitCheck =
        LoggerMessage.Define<string, CircuitState>(LogLevel.Information,
            new EventId(1, "ExecuteAsync"),
            "Platform: {Platform}, Circuit State: {State}");

    private static readonly Action<ILogger, string, string, Exception?> _logDlqEnqueued =
        LoggerMessage.Define<string, string>(LogLevel.Error,
            new EventId(2, "EnqueueToDeadLetterQueue"),
            "Operation failed for {Platform}, enqueued to DLQ: {Error}");

    private static readonly Action<ILogger, string, Exception?> _logDlqFailed =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(3, "EnqueueToDeadLetterQueue"),
            "Failed to enqueue to DLQ for {Platform}");

    private readonly IJobScraper _innerScraper;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IGenericDeadLetterQueue _deadLetterQueue;
    private readonly ILogger<ResilientJobScraper> _logger;

    public string PlatformName => _innerScraper.PlatformName;

    public ResilientJobScraper(
        IJobScraper innerScraper,
        ICircuitBreaker circuitBreaker,
        IGenericDeadLetterQueue deadLetterQueue,
        ILogger<ResilientJobScraper> logger)
    {
        _innerScraper = innerScraper ?? throw new ArgumentNullException(nameof(innerScraper));
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ResilientJobScraper>.Instance;
    }

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        return ExecuteWithResilienceAsync(() => _innerScraper.SearchJobsAsync(criteria, ct), criteria, ct);
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        return ExecuteWithResilienceAsync(() => _innerScraper.GetJobDetailsAsync(jobId, ct), jobId, ct);
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        return ExecuteWithResilienceAsync(() => _innerScraper.ApplyAsync(jobId, details, ct), jobId, ct);
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        string filterKey = filter?.ToString() ?? "all";
        return ExecuteWithResilienceAsync(() => _innerScraper.GetApplicationsAsync(filter, ct), filterKey, ct);
    }

    public async Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        await ExecuteWithResilienceAsync(async () =>
        {
            await _innerScraper.SaveJobAsync(jobId, ct).ConfigureAwait(false);
            return true;
        }, jobId, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        return ExecuteWithResilienceAsync(() => _innerScraper.GetSavedJobsAsync(ct), "getAll", ct);
    }

    private async Task<T> ExecuteWithResilienceAsync<T>(Func<Task<T>> operation, object operationKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operationKey);

        _logCircuitCheck(_logger, PlatformName, _circuitBreaker.State, null);

        try
        {
            return await _circuitBreaker.ExecuteAsync(operation).ConfigureAwait(false);
        }
        catch (Exception ex) when (_circuitBreaker.State == CircuitState.Open)
        {
            await EnqueueToDeadLetterQueueAsync(operationKey, ex, ct).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await EnqueueToDeadLetterQueueAsync(operationKey, ex, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task EnqueueToDeadLetterQueueAsync(object operationKey, Exception exception, CancellationToken ct)
    {
        try
        {
            await _deadLetterQueue.EnqueueAsync(
                operationKey,
                exception.Message,
                exception,
                ct).ConfigureAwait(false);

            _logDlqEnqueued(_logger, PlatformName, exception.Message, exception);
        }
        catch (Exception dlqEx)
        {
            _logDlqFailed(_logger, PlatformName, dlqEx);
        }
    }
}
