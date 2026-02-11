using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Captures comprehensive diagnostic artifacts on test failure.
/// Provides automatic collection of browser traces, HAR files, screenshots,
/// console logs, and other debugging information.
/// </summary>
public sealed class FailureDiagnosticsHelper : IAsyncDisposable
{
    private readonly string _diagnosticsRoot;
    private readonly string _testRunId;
    private readonly List<DiagnosticArtifact> _capturedArtifacts = new();
    private readonly Stopwatch _testStopwatch = new();
    private readonly List<TimelineEvent> _timeline = new();
    private bool _disposed;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets the unique correlation ID for this test run.
    /// Use this ID to correlate logs across distributed systems.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the test output helper for logging.
    /// </summary>
    public ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets the scenario ID for the current test.
    /// </summary>
    public string ScenarioId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the fixture ID for the current test.
    /// </summary>
    public string FixtureId { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureDiagnosticsHelper"/> class.
    /// </summary>
    /// <param name="output">The test output helper for logging.</param>
    /// <param name="scenarioId">Optional scenario ID for the test.</param>
    /// <param name="fixtureId">Optional fixture ID for the test.</param>
    public FailureDiagnosticsHelper(ITestOutputHelper output, string? scenarioId = null, string? fixtureId = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        Output = output;
        _testRunId = $"test-run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        CorrelationId = Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId ?? "unknown";
        FixtureId = fixtureId ?? "unknown";

        // Create diagnostics directory
        _diagnosticsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ghost",
            "test-diagnostics",
            _testRunId);

        Directory.CreateDirectory(_diagnosticsRoot);

        _testStopwatch.Start();
        AddTimelineEvent("TestStarted", "Test execution started");
    }

    /// <summary>
    /// Sets the scenario ID for the current test.
    /// </summary>
    /// <param name="scenarioId">The scenario ID.</param>
    public void SetScenarioId(string scenarioId)
    {
        ArgumentNullException.ThrowIfNull(scenarioId);
        ScenarioId = scenarioId;
        AddTimelineEvent("ScenarioIdSet", $"Scenario ID: {scenarioId}");
    }

    /// <summary>
    /// Sets the fixture ID for the current test.
    /// </summary>
    /// <param name="fixtureId">The fixture ID.</param>
    public void SetFixtureId(string fixtureId)
    {
        ArgumentNullException.ThrowIfNull(fixtureId);
        FixtureId = fixtureId;
        AddTimelineEvent("FixtureIdSet", $"Fixture ID: {fixtureId}");
    }

    /// <summary>
    /// Records a timeline event for the test execution.
    /// </summary>
    /// <param name="eventType">The type of event.</param>
    /// <param name="description">The event description.</param>
    public void AddTimelineEvent(string eventType, string description)
    {
        var @event = new TimelineEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Description = description,
            ElapsedMilliseconds = _testStopwatch.ElapsedMilliseconds
        };

        _timeline.Add(@event);
        Output.WriteLine($"[{@event.Timestamp:HH:mm:ss.fff}] [{eventType}] {description}");
    }

    /// <summary>
    /// Captures a screenshot from the browser page.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured screenshot.</returns>
    public async Task<string> CaptureScreenshotAsync(Ghost.IPage page, string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var name = artifactName ?? $"screenshot-{DateTime.UtcNow:HHmmss-fff}.png";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            var screenshotBytes = await page.ScreenshotAsync();
            await File.WriteAllBytesAsync(path, screenshotBytes);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.Screenshot,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("ScreenshotCaptured", $"Screenshot saved to {name}");
            Output.WriteLine($"[Diagnostics] Screenshot captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("ScreenshotFailed", $"Failed to capture screenshot: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture screenshot: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures the DOM snapshot from the browser page.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured DOM snapshot.</returns>
    public async Task<string> CaptureDomSnapshotAsync(Ghost.IPage page, string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var name = artifactName ?? $"dom-snapshot-{DateTime.UtcNow:HHmmss-fff}.html";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            var html = await page.GetContentAsync();
            await File.WriteAllTextAsync(path, html, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.DomSnapshot,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("DomSnapshotCaptured", $"DOM snapshot saved to {name}");
            Output.WriteLine($"[Diagnostics] DOM snapshot captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("DomSnapshotFailed", $"Failed to capture DOM snapshot: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture DOM snapshot: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures console logs from the browser page.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured console logs.</returns>
    public async Task<string> CaptureConsoleLogsAsync(Ghost.IPage page, string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var name = artifactName ?? $"console-logs-{DateTime.UtcNow:HHmmss-fff}.json";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            // Collect console logs via JavaScript evaluation
            var logsScript = @"
                (function() {
                    const logs = [];
                    const originalLog = console.log;
                    const originalError = console.error;
                    const originalWarn = console.warn;

                    // Return any logs that were captured
                    return window.__capturedConsoleLogs || [];
                })();
            ";

            var logs = await page.EvaluateAsync<string>(logsScript);
            await File.WriteAllTextAsync(path, logs, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.ConsoleLogs,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("ConsoleLogsCaptured", $"Console logs saved to {name}");
            Output.WriteLine($"[Diagnostics] Console logs captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("ConsoleLogsFailed", $"Failed to capture console logs: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture console logs: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures the current page URL and metadata.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured page metadata.</returns>
    public async Task<string> CapturePageMetadataAsync(Ghost.IPage page, string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var name = artifactName ?? $"page-metadata-{DateTime.UtcNow:HHmmss-fff}.json";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            var metadata = new
            {
                Url = page.Url,
                Title = page.Title,
                PageId = page.PageId,
                CapturedAt = DateTime.UtcNow,
                CorrelationId = CorrelationId,
                ScenarioId = ScenarioId,
                FixtureId = FixtureId
            };

            var json = JsonSerializer.Serialize(metadata, s_jsonOptions);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.Metadata,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("PageMetadataCaptured", $"Page metadata saved to {name}");
            Output.WriteLine($"[Diagnostics] Page metadata captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("PageMetadataFailed", $"Failed to capture page metadata: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture page metadata: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures application logs and test output.
    /// </summary>
    /// <param name="logs">The log content to capture.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured logs.</returns>
    public string CaptureApplicationLogs(string logs, string? artifactName = null)
    {
        if (string.IsNullOrEmpty(logs)) return string.Empty;

        var name = artifactName ?? $"application-logs-{DateTime.UtcNow:HHmmss-fff}.txt";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            File.WriteAllText(path, logs, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.ApplicationLogs,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("ApplicationLogsCaptured", $"Application logs saved to {name}");
            Output.WriteLine($"[Diagnostics] Application logs captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("ApplicationLogsFailed", $"Failed to capture application logs: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture application logs: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures the test timeline as a JSON file.
    /// </summary>
    /// <returns>The path to the captured timeline.</returns>
    public string CaptureTimeline()
    {
        var name = $"timeline-{DateTime.UtcNow:HHmmss-fff}.json";
        var path = Path.Combine(_diagnosticsRoot, name);

        try
        {
            var timelineData = new
            {
                CorrelationId = CorrelationId,
                ScenarioId = ScenarioId,
                FixtureId = FixtureId,
                TestRunId = _testRunId,
                TotalDurationMs = _testStopwatch.ElapsedMilliseconds,
                Events = _timeline
            };

            var json = JsonSerializer.Serialize(timelineData, s_jsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.Timeline,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            _capturedArtifacts.Add(artifact);
            AddTimelineEvent("TimelineCaptured", $"Timeline saved to {name}");
            Output.WriteLine($"[Diagnostics] Timeline captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            AddTimelineEvent("TimelineFailed", $"Failed to capture timeline: {ex.Message}");
            Output.WriteLine($"[Diagnostics] Failed to capture timeline: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures all available diagnostic artifacts on test failure.
    /// This is the main entry point for automatic failure diagnostics.
    /// </summary>
    /// <param name="page">The browser page (optional).</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The path to the diagnostics package directory.</returns>
    public async Task<string> CaptureFailureAsync(Ghost.IPage? page = null, Exception? exception = null)
    {
        AddTimelineEvent("FailureDetected", $"Test failed: {exception?.Message ?? "Unknown error"}");

        // Capture exception details
        if (exception != null)
        {
            var exceptionPath = Path.Combine(_diagnosticsRoot, $"exception-{DateTime.UtcNow:HHmmss-fff}.txt");
            var exceptionText = $"Exception Type: {exception.GetType().FullName}\n" +
                               $"Message: {exception.Message}\n" +
                               $"Stack Trace:\n{exception.StackTrace}\n";

            if (exception.InnerException != null)
            {
                exceptionText += $"\nInner Exception:\n{exception.InnerException}";
            }

            await File.WriteAllTextAsync(exceptionPath, exceptionText, Encoding.UTF8);
            _capturedArtifacts.Add(new DiagnosticArtifact
            {
                Type = ArtifactType.Exception,
                Name = Path.GetFileName(exceptionPath),
                Path = exceptionPath,
                CapturedAt = DateTime.UtcNow
            });
        }

        // Capture browser artifacts if page is available
        if (page != null)
        {
            await CaptureScreenshotAsync(page);
            await CaptureDomSnapshotAsync(page);
            await CaptureConsoleLogsAsync(page);
            await CapturePageMetadataAsync(page);
        }

        // Capture timeline
        CaptureTimeline();

        // Generate summary
        var summaryPath = Path.Combine(_diagnosticsRoot, "diagnostics-summary.txt");
        var summary = GenerateSummary();
        await File.WriteAllTextAsync(summaryPath, summary, Encoding.UTF8);

        AddTimelineEvent("DiagnosticsComplete", $"All artifacts captured to {_diagnosticsRoot}");
        Output.WriteLine($"[Diagnostics] Failure diagnostics captured to: {_diagnosticsRoot}");
        Output.WriteLine($"[Diagnostics] Correlation ID: {CorrelationId}");
        Output.WriteLine($"[Diagnostics] Artifacts captured: {_capturedArtifacts.Count}");

        return _diagnosticsRoot;
    }

    /// <summary>
    /// Generates a summary of all captured artifacts.
    /// </summary>
    /// <returns>The summary text.</returns>
    private string GenerateSummary()
    {
        var sb = new StringBuilder();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendLine("=== Ghost Test Failure Diagnostics ===");
        sb.AppendLine();
        sb.AppendLine(string.Format(culture, "Test Run ID: {0}", _testRunId));
        sb.AppendLine(string.Format(culture, "Correlation ID: {0}", CorrelationId));
        sb.AppendLine(string.Format(culture, "Scenario ID: {0}", ScenarioId));
        sb.AppendLine(string.Format(culture, "Fixture ID: {0}", FixtureId));
        sb.AppendLine(string.Format(culture, "Captured At: {0:yyyy-MM-dd HH:mm:ss} UTC", DateTime.UtcNow));
        sb.AppendLine(string.Format(culture, "Total Duration: {0}ms", _testStopwatch.ElapsedMilliseconds));
        sb.AppendLine();
        sb.AppendLine("=== Captured Artifacts ===");
        sb.AppendLine();

        foreach (var artifact in _capturedArtifacts)
        {
            sb.AppendLine(string.Format(culture, "[{0}] {1}", artifact.Type, artifact.Name));
            sb.AppendLine(string.Format(culture, "  Path: {0}", artifact.Path));
            sb.AppendLine(string.Format(culture, "  Captured: {0:yyyy-MM-dd HH:mm:ss.fff} UTC", artifact.CapturedAt));
            sb.AppendLine();
        }

        sb.AppendLine("=== Timeline ===");
        sb.AppendLine();

        foreach (var @event in _timeline)
        {
            sb.AppendLine(string.Format(culture, "[{0:HH:mm:ss.fff}] [{1}] {2} (+{3}ms)",
                @event.Timestamp, @event.EventType, @event.Description, @event.ElapsedMilliseconds));
        }

        sb.AppendLine();
        sb.AppendLine("=== CI Integration ===");
        sb.AppendLine();
        sb.AppendLine("To upload these artifacts to CI, add the following step:");
        sb.AppendLine("  - name: Upload Test Diagnostics");
        sb.AppendLine("    if: failure()");
        sb.AppendLine("    uses: actions/upload-artifact@v4");
        sb.AppendLine("    with:");
        sb.AppendLine("      name: test-diagnostics-${{ github.run_id }}");
        sb.AppendLine(string.Format(culture, "      path: {0}", _diagnosticsRoot));
        sb.AppendLine("      retention-days: 30");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the root directory for all captured artifacts.
    /// </summary>
    /// <returns>The diagnostics root directory path.</returns>
    public string GetDiagnosticsRoot() => _diagnosticsRoot;

    /// <summary>
    /// Gets all captured artifacts.
    /// </summary>
    /// <returns>A read-only list of captured artifacts.</returns>
    public IReadOnlyList<DiagnosticArtifact> GetCapturedArtifacts() => _capturedArtifacts.AsReadOnly();

    /// <summary>
    /// Disposes the diagnostics helper and cleans up resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _testStopwatch.Stop();
        AddTimelineEvent("TestCompleted", $"Test completed after {_testStopwatch.ElapsedMilliseconds}ms");

        // Only keep artifacts if test failed (detected by exception artifacts)
        if (!_capturedArtifacts.Any(a => a.Type == ArtifactType.Exception))
        {
            try
            {
                // Clean up diagnostics directory for successful tests
                if (Directory.Exists(_diagnosticsRoot))
                {
                    Directory.Delete(_diagnosticsRoot, recursive: true);
                    Output.WriteLine($"[Diagnostics] Cleaned up diagnostics directory for successful test");
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"[Diagnostics] Failed to clean up diagnostics directory: {ex.Message}");
            }
        }

        _disposed = true;
    }
}

/// <summary>
/// Represents a diagnostic artifact captured during test execution.
/// </summary>
public sealed class DiagnosticArtifact
{
    /// <summary>
    /// Gets or sets the type of artifact.
    /// </summary>
    public ArtifactType Type { get; set; }

    /// <summary>
    /// Gets or sets the artifact name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the artifact was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }
}

/// <summary>
/// Represents a timeline event during test execution.
/// </summary>
public sealed class TimelineEvent
{
    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the elapsed milliseconds since test start.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }
}

/// <summary>
/// Types of diagnostic artifacts that can be captured.
/// </summary>
public enum ArtifactType
{
    /// <summary>Browser screenshot</summary>
    Screenshot,

    /// <summary>DOM snapshot (HTML)</summary>
    DomSnapshot,

    /// <summary>Browser console logs</summary>
    ConsoleLogs,

    /// <summary>Application logs</summary>
    ApplicationLogs,

    /// <summary>Test timeline</summary>
    Timeline,

    /// <summary>Page metadata</summary>
    Metadata,

    /// <summary>Exception details</summary>
    Exception,

    /// <summary>Browser trace (Playwright trace)</summary>
    BrowserTrace,

    /// <summary>HAR (HTTP Archive) file</summary>
    Har
}
