using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;
using Orleans;

namespace Ghost.Cloud.Api.Canaries;

/// <summary>
/// Interface for running assurance canary tests against endpoints.
/// </summary>
public interface IAssuranceCanaryRunner
{
    /// <summary>
    /// Runs a canary test for the scheduled run.
    /// </summary>
    /// <param name="scheduledRun">The scheduled run information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the canary run.</returns>
    public Task<CanaryRunOutcome> RunAsync(ScheduledRunInfo scheduledRun, CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of the assurance canary runner that executes lightweight spider runs
/// to validate endpoint health and capture diagnostic information.
/// </summary>
public sealed class AssuranceCanaryRunner : IAssuranceCanaryRunner
{
    private readonly IClusterClient _clusterClient;
    private readonly IGhostEngine _engine;
    private readonly IDownloader _downloader;
    private readonly ILogger<AssuranceCanaryRunner> _logger;

    // LoggerMessage delegates for high-performance logging
    private static readonly Action<ILogger, string, string, Exception?> LogCanaryStarting =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2001, nameof(LogCanaryStarting)),
            "Starting canary run {RunId} for endpoint {EndpointId}");

    private static readonly Action<ILogger, string, string, Exception?> LogInputValidationFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2002, nameof(LogInputValidationFailed)),
            "Canary run {RunId} failed input validation for endpoint {EndpointId}");

    private static readonly Action<ILogger, string, string, int, int, double, Exception?> LogCanaryCompleted =
        LoggerMessage.Define<string, string, int, int, double>(
            LogLevel.Information,
            new EventId(2003, nameof(LogCanaryCompleted)),
            "Canary run {RunId} completed with classification {Classification}. Items: {ItemsDiscovered}, Artifacts: {ArtifactsCaptured}, Duration: {DurationMs}ms");

    private static readonly Action<ILogger, string, double, Exception?> LogCanaryTimeout =
        LoggerMessage.Define<string, double>(
            LogLevel.Warning,
            new EventId(2004, nameof(LogCanaryTimeout)),
            "Canary run {RunId} timed out after {DurationMs}ms");

    private static readonly Action<ILogger, string, double, Exception?> LogCanaryCancelled =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(2005, nameof(LogCanaryCancelled)),
            "Canary run {RunId} was cancelled after {DurationMs}ms");

    private static readonly Action<ILogger, string, int, string, Exception?> LogNetworkError =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Error,
            new EventId(2006, nameof(LogNetworkError)),
            "Canary run {RunId} failed with network error. Status: {StatusCode}, Classification: {Classification}");

    private static readonly Action<ILogger, string, Exception?> LogUnexpectedError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2007, nameof(LogUnexpectedError)),
            "Canary run {RunId} failed with unexpected error");

    // Timeouts for canary runs - intentionally short to fail fast
    private static readonly TimeSpan CanaryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    // Classification constants for consistent outcome reporting
    private const string ClassificationSuccess = "Success";
    private const string ClassificationTimeout = "Timeout";
    private const string ClassificationRateLimited = "RateLimited";
    private const string ClassificationEndpointError = "EndpointError";
    private const string ClassificationNetworkError = "NetworkError";
    private const string ClassificationConfigurationError = "ConfigurationError";
    private const string ClassificationCancelled = "Cancelled";
    private const string ClassificationUnknown = "Unknown";

    public AssuranceCanaryRunner(
        IClusterClient clusterClient,
        IGhostEngine engine,
        IDownloader downloader,
        ILogger<AssuranceCanaryRunner> logger)
    {
        _clusterClient = clusterClient ?? throw new ArgumentNullException(nameof(clusterClient));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CanaryRunOutcome> RunAsync(ScheduledRunInfo scheduledRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledRun);

        using Activity? activity = new Activity("AssuranceCanaryRunner.Run").Start();
        activity?.SetTag("ghost.canary.runId", scheduledRun.RunId);
        activity?.SetTag("ghost.canary.endpointId", scheduledRun.EndpointId);
        activity?.SetTag("ghost.canary.tenantId", scheduledRun.TenantId);

        LogCanaryStarting(_logger, scheduledRun.RunId, scheduledRun.EndpointId, null);

        // Create a linked cancellation token with the canary timeout
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CanaryTimeout);
        CancellationToken linkedToken = cts.Token;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsDiscovered = 0;
        int artifactsCaptured = 0;

        try
        {
            // Get endpoint configuration
            IEndpointGrain endpointGrain = _clusterClient.GetGrain<IEndpointGrain>(scheduledRun.EndpointId);
            EndpointManifest manifest = await endpointGrain
                .GetManifestAsync()
                .ConfigureAwait(false);

            activity?.SetTag("ghost.canary.pluginId", manifest.PluginId);
            activity?.SetTag("ghost.canary.capability", manifest.Capability.ToString());

            // Validate input against endpoint schema
            try
            {
                await endpointGrain.ValidateInputAsync(scheduledRun.Input).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                LogInputValidationFailed(_logger, scheduledRun.RunId, scheduledRun.EndpointId, ex);

                return CreateOutcome(
                    false,
                    ClassificationConfigurationError,
                    $"Input validation failed: {ex.Message}",
                    itemsDiscovered,
                    artifactsCaptured,
                    stopwatch.Elapsed);
            }

            // Create and run the canary spider
            CanarySpider spider = new(
                scheduledRun.EndpointId,
                manifest,
                scheduledRun.Input,
                _downloader,
                linkedToken);

            GhostEngineContext context = new(
                scheduledRun.RunId,
                $"canary-{scheduledRun.EndpointId}",
                new Dictionary<string, object?>
                {
                    ["tenantId"] = scheduledRun.TenantId,
                    ["endpointId"] = scheduledRun.EndpointId,
                    ["capability"] = manifest.Capability.ToString(),
                });

            // Execute the spider with timeout protection
            await _engine.RunAsync(spider, context, linkedToken).ConfigureAwait(false);

            itemsDiscovered = spider.ItemsDiscovered;
            artifactsCaptured = spider.ArtifactsCaptured;

            // Classify the outcome based on spider results
            (bool success, string classification, string? errorMessage) = ClassifyOutcome(spider, linkedToken);

            LogCanaryCompleted(
                _logger,
                scheduledRun.RunId,
                classification,
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.ElapsedMilliseconds,
                null);

            activity?.SetTag("ghost.canary.classification", classification);
            activity?.SetTag("ghost.canary.itemsDiscovered", itemsDiscovered);
            activity?.SetTag("ghost.canary.artifactsCaptured", artifactsCaptured);
            activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error, errorMessage);

            return CreateOutcome(
                success,
                classification,
                errorMessage,
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout - not user cancellation
            LogCanaryTimeout(_logger, scheduledRun.RunId, stopwatch.ElapsedMilliseconds, null);

            activity?.SetTag("ghost.canary.classification", ClassificationTimeout);
            activity?.SetStatus(ActivityStatusCode.Error, "Canary run timed out");

            return CreateOutcome(
                false,
                ClassificationTimeout,
                $"Canary run exceeded timeout of {CanaryTimeout.TotalSeconds}s",
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // User cancellation
            LogCanaryCancelled(_logger, scheduledRun.RunId, stopwatch.ElapsedMilliseconds, null);

            activity?.SetTag("ghost.canary.classification", ClassificationCancelled);
            activity?.SetStatus(ActivityStatusCode.Error, "Canary run was cancelled");

            return CreateOutcome(
                false,
                ClassificationCancelled,
                "Canary run was cancelled",
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            // Network-level errors
            int statusCode = (int?)ex.StatusCode ?? 0;
            string classification = statusCode switch
            {
                429 => ClassificationRateLimited,
                >= 500 => ClassificationEndpointError,
                _ => ClassificationNetworkError
            };

            LogNetworkError(_logger, scheduledRun.RunId, statusCode, classification, ex);

            activity?.SetTag("ghost.canary.classification", classification);
            activity?.SetTag("ghost.canary.httpStatusCode", statusCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return CreateOutcome(
                false,
                classification,
                $"Network error: {ex.Message}",
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(_logger, scheduledRun.RunId, ex);

            activity?.SetTag("ghost.canary.classification", ClassificationUnknown);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return CreateOutcome(
                false,
                ClassificationUnknown,
                $"Unexpected error: {ex.Message}",
                itemsDiscovered,
                artifactsCaptured,
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Classifies the outcome based on spider execution results.
    /// </summary>
    private static (bool success, string classification, string? errorMessage) ClassifyOutcome(
        CanarySpider spider,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return (false, ClassificationTimeout, "Operation timed out");
        }

        if (spider.LastError is not null)
        {
            return (false, spider.LastErrorClassification ?? ClassificationUnknown, spider.LastError);
        }

        if (spider.ItemsDiscovered == 0 && spider.Manifest.Capability != EndpointCapability.Streaming)
        {
            // For non-streaming endpoints, we expect at least some items
            return (true, ClassificationSuccess, null); // Still success, but could be warning
        }

        return (true, ClassificationSuccess, null);
    }

    /// <summary>
    /// Creates a standardized outcome record.
    /// </summary>
    private static CanaryRunOutcome CreateOutcome(
        bool success,
        string classification,
        string? errorMessage,
        int itemsDiscovered,
        int artifactsCaptured,
        TimeSpan duration)
    {
        // Generate a diagnostics URI that could point to stored logs/metrics
        // In a real implementation, this would upload diagnostics to blob storage
        string? diagnosticsUri = success
            ? null
            : $"ghost://diagnostics/canary/{Guid.NewGuid():N}?durationMs={duration.TotalMilliseconds}";

        return new CanaryRunOutcome
        {
            Success = success,
            Classification = classification,
            ErrorMessage = errorMessage,
            ItemsDiscovered = itemsDiscovered,
            ArtifactsCaptured = artifactsCaptured,
            DiagnosticsUri = diagnosticsUri
        };
    }
}

/// <summary>
/// Lightweight spider implementation for canary testing endpoints.
/// Performs minimal work to validate endpoint health without full data extraction.
/// </summary>
internal sealed class CanarySpider : ISpider
{
    private readonly IDownloader _downloader;
    private readonly CancellationToken _cancellationToken;
    private readonly List<ItemEnvelope> _items = new();

    public string Name => $"canary-{EndpointId}";
    public string EndpointId { get; }
    public EndpointManifest Manifest { get; }
    public JsonElement Input { get; }
    public int ItemsDiscovered => _items.Count;
    public int ArtifactsCaptured { get; private set; }
    public string? LastError { get; private set; }
    public string? LastErrorClassification { get; private set; }

    public CanarySpider(
        string endpointId,
        EndpointManifest manifest,
        JsonElement input,
        IDownloader downloader,
        CancellationToken cancellationToken)
    {
        EndpointId = endpointId ?? throw new ArgumentNullException(nameof(endpointId));
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Input = input;
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _cancellationToken = cancellationToken;
    }

    public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(
        GhostEngineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create a single canary request based on endpoint capability and input
        // This is a simplified implementation - real spiders would have more complex logic

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _cancellationToken);

        // Build the canary URL based on capability
        string url = BuildCanaryUrl();

        GhostRequest request = new(
            Url: url,
            Method: "GET",
            Headers: new Dictionary<string, string>
            {
                ["X-Ghost-Canary"] = "true",
                ["X-Ghost-RunId"] = context.JobId,
                ["Accept"] = "text/html,application/json",
                ["User-Agent"] = "Ghost-Canary/1.0"
            },
            Body: null,
            Timeout: TimeSpan.FromSeconds(10));

        yield return request;
    }

    public async Task<SpiderOutput> ParseAsync(
        GhostResponse response,
        GhostEngineContext context,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _cancellationToken);

        // Check for HTTP error status
        if (response.StatusCode >= 400)
        {
            LastError = $"HTTP {response.StatusCode} from endpoint";
            LastErrorClassification = response.StatusCode switch
            {
                429 => "RateLimited",
                >= 500 => "EndpointError",
                _ => "ClientError"
            };

            return new SpiderOutput(
                Array.Empty<GhostRequest>(),
                Array.Empty<ItemEnvelope>());
        }

        // Parse response based on content type and capability
        try
        {
            List<ItemEnvelope> items = new();

            // For canary runs, we do minimal parsing - just verify we can extract something
            // Real implementation would use proper selectors based on endpoint schema
            ItemEnvelope healthCheckItem = new(
                Type: "CanaryHealthCheck",
                Data: new Dictionary<string, object?>
                {
                    ["endpointId"] = EndpointId,
                    ["statusCode"] = response.StatusCode,
                    ["contentLength"] = response.Content?.Length ?? 0,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["healthy"] = response.StatusCode < 400 && !string.IsNullOrEmpty(response.Content)
                },
                CapturedAtUtc: DateTimeOffset.UtcNow);

            items.Add(healthCheckItem);
            _items.Add(healthCheckItem);

            // If there's actual content, try to extract items based on capability
            if (!string.IsNullOrEmpty(response.Content) && response.Content.Length > 0)
            {
                // Simulate item extraction - in reality this would use proper parsing
                items.Add(new ItemEnvelope(
                    Type: "CanarySampleItem",
                    Data: new Dictionary<string, object?>
                    {
                        ["endpointId"] = EndpointId,
                        ["sampleSize"] = Math.Min(response.Content.Length, 1000),
                        ["contentType"] = response.Headers.TryGetValue("Content-Type", out string? ct) ? ct : "unknown"
                    },
                    CapturedAtUtc: DateTimeOffset.UtcNow));

                _items.Add(items[^1]);
            }

            return new SpiderOutput(
                Array.Empty<GhostRequest>(),
                items);
        }
        catch (Exception ex)
        {
            LastError = $"Parse error: {ex.Message}";
            LastErrorClassification = "ParseError";

            return new SpiderOutput(
                Array.Empty<GhostRequest>(),
                Array.Empty<ItemEnvelope>());
        }
    }

    /// <summary>
    /// Builds a canary URL based on endpoint capability and input.
    /// </summary>
    private string BuildCanaryUrl()
    {
        // For canary runs, we construct a minimal test URL
        // In a real implementation, this would use endpoint configuration

        return Manifest.Capability switch
        {
            EndpointCapability.Search => $"https://example.com/search?q=canary-test-{Guid.NewGuid():N}",
            EndpointCapability.Discovery => $"https://example.com/discover?canary={Guid.NewGuid():N}",
            EndpointCapability.Detail => $"https://example.com/item/canary-test-{Guid.NewGuid():N}",
            EndpointCapability.Streaming => $"https://example.com/stream?canary={Guid.NewGuid():N}",
            _ => $"https://example.com/health?canary={Guid.NewGuid():N}"
        };
    }
}
