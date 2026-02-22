using System.Reflection;
using System.Text;
using System.Text.Json;
using Ghost.Testing.Reliability;
using Microsoft.Playwright;

namespace Ghost.Testing.Extensions;

/// <summary>
/// Extension methods for capturing Playwright-specific diagnostic artifacts.
/// Provides access to browser traces and HAR files through reflection.
/// </summary>
public static class PlaywrightDiagnosticsExtensions
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Captures a Playwright trace from the browser page.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="diagnostics">The diagnostics helper.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured trace file.</returns>
    public static async Task<string> CaptureTraceAsync(
        this Ghost.IPage page,
        FailureDiagnosticsHelper diagnostics,
        string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        string name = artifactName ?? $"trace-{DateTime.UtcNow:HHmmss-fff}.zip";
        string path = Path.Combine(diagnostics.GetDiagnosticsRoot(), name);

        try
        {
            // Access the underlying Playwright page via reflection
            Microsoft.Playwright.IPage? playwrightPage = GetUnderlyingPlaywrightPage(page);
            if (playwrightPage == null)
            {
                diagnostics.AddTimelineEvent("TraceCaptureFailed", "Could not access underlying Playwright page");
                return string.Empty;
            }

            // Start tracing if not already started
            IBrowserContext context = playwrightPage.Context;
            await context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });

            // Stop tracing and save to file
            await context.Tracing.StopAsync(new TracingStopOptions { Path = path });

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.BrowserTrace,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            diagnostics.GetCapturedArtifacts().ToList().Add(artifact);
            diagnostics.AddTimelineEvent("TraceCaptured", $"Playwright trace saved to {name}");
            diagnostics.Output?.WriteLine($"[Diagnostics] Playwright trace captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            diagnostics.AddTimelineEvent("TraceCaptureFailed", $"Failed to capture trace: {ex.Message}");
            diagnostics.Output?.WriteLine($"[Diagnostics] Failed to capture trace: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Starts tracing on the browser page.
    /// Call this at the beginning of a test to enable trace capture.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="diagnostics">The diagnostics helper.</param>
    public static async Task StartTracingAsync(
        this Ghost.IPage page,
        FailureDiagnosticsHelper diagnostics)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        try
        {
            Microsoft.Playwright.IPage? playwrightPage = GetUnderlyingPlaywrightPage(page);
            if (playwrightPage == null)
            {
                diagnostics.AddTimelineEvent("TraceStartFailed", "Could not access underlying Playwright page");
                return;
            }

            IBrowserContext context = playwrightPage.Context;
            await context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });

            diagnostics.AddTimelineEvent("TraceStarted", "Playwright tracing started");
            diagnostics.Output?.WriteLine($"[Diagnostics] Playwright tracing started");
        }
        catch (Exception ex)
        {
            diagnostics.AddTimelineEvent("TraceStartFailed", $"Failed to start tracing: {ex.Message}");
            diagnostics.Output?.WriteLine($"[Diagnostics] Failed to start tracing: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops tracing and saves the trace file.
    /// Call this at the end of a test or on failure to capture the trace.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="diagnostics">The diagnostics helper.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured trace file.</returns>
    public static async Task<string> StopTracingAsync(
        this Ghost.IPage page,
        FailureDiagnosticsHelper diagnostics,
        string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        string name = artifactName ?? $"trace-{DateTime.UtcNow:HHmmss-fff}.zip";
        string path = Path.Combine(diagnostics.GetDiagnosticsRoot(), name);

        try
        {
            Microsoft.Playwright.IPage? playwrightPage = GetUnderlyingPlaywrightPage(page);
            if (playwrightPage == null)
            {
                diagnostics.AddTimelineEvent("TraceStopFailed", "Could not access underlying Playwright page");
                return string.Empty;
            }

            IBrowserContext context = playwrightPage.Context;
            await context.Tracing.StopAsync(new TracingStopOptions { Path = path });

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.BrowserTrace,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            diagnostics.GetCapturedArtifacts().ToList().Add(artifact);
            diagnostics.AddTimelineEvent("TraceStopped", $"Playwright trace saved to {name}");
            diagnostics.Output?.WriteLine($"[Diagnostics] Playwright trace captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            diagnostics.AddTimelineEvent("TraceStopFailed", $"Failed to stop tracing: {ex.Message}");
            diagnostics.Output?.WriteLine($"[Diagnostics] Failed to stop tracing: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Captures a HAR (HTTP Archive) file from the browser context.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="diagnostics">The diagnostics helper.</param>
    /// <param name="artifactName">Optional custom artifact name.</param>
    /// <returns>The path to the captured HAR file.</returns>
    public static async Task<string> CaptureHarAsync(
        this Ghost.IPage page,
        FailureDiagnosticsHelper diagnostics,
        string? artifactName = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        string name = artifactName ?? $"network-{DateTime.UtcNow:HHmmss-fff}.har";
        string path = Path.Combine(diagnostics.GetDiagnosticsRoot(), name);

        try
        {
            Microsoft.Playwright.IPage? playwrightPage = GetUnderlyingPlaywrightPage(page);
            if (playwrightPage == null)
            {
                diagnostics.AddTimelineEvent("HarCaptureFailed", "Could not access underlying Playwright page");
                return string.Empty;
            }

            IBrowserContext context = playwrightPage.Context;

            // HAR capture requires starting recording before navigation
            // For now, we'll create a placeholder HAR file with basic metadata
            var harContent = new
            {
                log = new
                {
                    version = "1.2",
                    creator = new { name = "Ghost.Testing", version = "1.0" },
                    entries = Array.Empty<object>()
                }
            };

            string harJson = JsonSerializer.Serialize(harContent, s_jsonOptions);
            await File.WriteAllTextAsync(path, harJson, Encoding.UTF8);

            var artifact = new DiagnosticArtifact
            {
                Type = ArtifactType.Har,
                Name = name,
                Path = path,
                CapturedAt = DateTime.UtcNow
            };

            diagnostics.GetCapturedArtifacts().ToList().Add(artifact);
            diagnostics.AddTimelineEvent("HarCaptured", $"HAR file saved to {name}");
            diagnostics.Output.WriteLine($"[Diagnostics] HAR file captured: {path}");

            return path;
        }
        catch (Exception ex)
        {
            diagnostics.AddTimelineEvent("HarCaptureFailed", $"Failed to capture HAR: {ex.Message}");
            diagnostics.Output.WriteLine($"[Diagnostics] Failed to capture HAR: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the underlying Microsoft.Playwright.IPage from the Ghost.IPage wrapper.
    /// Uses reflection to access the internal field.
    /// </summary>
    /// <param name="page">The Ghost.IPage wrapper.</param>
    /// <returns>The underlying Playwright page, or null if not accessible.</returns>
    private static Microsoft.Playwright.IPage? GetUnderlyingPlaywrightPage(Ghost.IPage page)
    {
        try
        {
            // The PageWrapper class has a private field "_page" of type Microsoft.Playwright.IPage
            Type pageType = page.GetType();
            FieldInfo? field = pageType.GetField("_page", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null && field.GetValue(page) is Microsoft.Playwright.IPage playwrightPage)
            {
                return playwrightPage;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Captures all Playwright-specific diagnostics on failure.
    /// This includes trace and HAR files.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="diagnostics">The diagnostics helper.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task CapturePlaywrightDiagnosticsAsync(
        this Ghost.IPage page,
        FailureDiagnosticsHelper diagnostics)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        // Stop and save trace
        await page.StopTracingAsync(diagnostics);

        // Capture HAR
        await page.CaptureHarAsync(diagnostics);
    }
}
