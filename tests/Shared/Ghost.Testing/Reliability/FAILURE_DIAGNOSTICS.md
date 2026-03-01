# Failure Diagnostics Capture

This document describes the failure diagnostics capture system for Ghost tests.

## Overview

The failure diagnostics system automatically captures comprehensive artifacts when tests fail, enabling one-click triage without re-running tests.

## Features

### Automatic Artifact Capture

On test failure, the following artifacts are automatically captured:

1. **Browser Screenshot** - PNG screenshot of the page at failure point
2. **DOM Snapshot** - Full HTML content of the page
3. **Console Logs** - Browser console output (errors, warnings, info)
4. **Page Metadata** - URL, title, page ID, correlation IDs
5. **Test Timeline** - Structured timeline of events with timestamps
6. **Exception Details** - Full exception with stack trace and inner exceptions
7. **Playwright Trace** - Browser trace file (`.zip`) for replay in Playwright Inspector
8. **HAR File** - HTTP Archive of network activity

### Correlation IDs

Every test run gets:
- **Correlation ID** - Unique ID for distributed tracing
- **Scenario ID** - Test class/method name
- **Fixture ID** - Fixture name
- **Test Run ID** - Timestamp-based unique identifier

These IDs appear in all logs and artifacts for easy correlation.

## Usage

### Basic Usage

Inherit from `ReliabilityTestBase` and use `CaptureFailureAsync` in catch blocks:

```csharp
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

public class MyTests : ReliabilityTestBase
{
    private readonly RealBrowserFixture _fixture;

    public MyTests(ITestOutputHelper output, RealBrowserFixture fixture)
        : base(output)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        try
        {
            var session = await _fixture.CreateSessionAsync();
            var page = await session.NewPageAsync();

            await page.NavigateAsync("https://example.com");

            // Your test logic here
        }
        catch (Exception ex)
        {
            // Automatically capture all diagnostics
            await CaptureFailureAsync(page, ex);
            throw;
        }
    }
}
```

### With Playwright Tracing

Enable Playwright tracing for detailed browser interaction replay:

```csharp
[Fact]
public async Task MyTestWithTracing()
{
    var session = await _fixture.CreateSessionAsync();
    var page = await session.NewPageAsync();

    // Start tracing at the beginning
    await page.StartTracingAsync(Diagnostics);

    try
    {
        await page.NavigateAsync("https://example.com");
        // Your test logic
    }
    catch (Exception ex)
    {
        // Stop tracing and capture all artifacts
        await page.StopTracingAsync(Diagnostics);
        await CaptureFailureAsync(page, ex);
        throw;
    }
}
```

### Manual Artifact Capture

Capture specific artifacts manually:

```csharp
[Fact]
public async Task MyTestManualCapture()
{
    var session = await _fixture.CreateSessionAsync();
    var page = await session.NewPageAsync();

    try
    {
        await page.NavigateAsync("https://example.com");
        // Your test logic
    }
    catch (Exception ex)
    {
        // Capture specific artifacts
        await Diagnostics.CaptureScreenshotAsync(page);
        await Diagnostics.CaptureDomSnapshotAsync(page);
        await Diagnostics.CaptureConsoleLogsAsync(page);
        await Diagnostics.CapturePageMetadataAsync(page);

        // Capture Playwright artifacts
        await page.StopTracingAsync(Diagnostics);
        await page.CaptureHarAsync(Diagnostics);

        // Capture failure with all artifacts
        await CaptureFailureAsync(page, ex);
        throw;
    }
}
```

### Timeline Events

Add custom timeline events for better debugging:

```csharp
[Fact]
public async Task MyTestWithTimeline()
{
    AddTimelineEvent("TestStarted", "Beginning test execution");

    var session = await _fixture.CreateSessionAsync();
    AddTimelineEvent("SessionCreated", $"Session ID: {session.SessionId}");

    var page = await session.NewPageAsync();
    AddTimelineEvent("PageCreated", $"Page ID: {page.PageId}");

    await page.NavigateAsync("https://example.com");
    AddTimelineEvent("NavigationComplete", $"URL: {page.Url}");

    // ... more test logic

    AddTimelineEvent("TestPassed", "All assertions passed");
}
```

## Artifact Storage

Artifacts are stored in:
```
~/.ghost/test-diagnostics/test-run-{timestamp}-{guid}/
├── screenshot-{timestamp}.png
├── dom-snapshot-{timestamp}.html
├── console-logs-{timestamp}.json
├── page-metadata-{timestamp}.json
├── timeline-{timestamp}.json
├── exception-{timestamp}.txt
├── trace-{timestamp}.zip
├── network-{timestamp}.har
└── diagnostics-summary.txt
```

### Cleanup

- **Successful tests**: Artifacts are automatically deleted
- **Failed tests**: Artifacts are retained for investigation

## CI Integration

### GitHub Actions

Add artifact upload on failure:

```yaml
- name: Run Tests
  run: dotnet test --no-build --filter "Category!=End2End&Capability!=RequiresProviderLive"

- name: Upload Test Diagnostics
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: test-diagnostics-${{ github.run_id }}
    path: ~/.ghost/test-diagnostics/
    retention-days: 30
```

### Azure Pipelines

```yaml
- script: dotnet test --no-build --filter "Category!=End2End&Capability!=RequiresProviderLive"
  displayName: 'Run Tests'

- task: PublishBuildArtifacts@1
  condition: failed()
  inputs:
    PathtoPublish: ~/.ghost/test-diagnostics/
    ArtifactName: test-diagnostics
    publishLocation: Container
```

## Viewing Artifacts

### Playwright Trace

1. Download the `trace-{timestamp}.zip` file
2. Open in [Playwright Inspector](https://playwright.dev/docs/trace-viewer):
   ```bash
   npx playwright show-trace trace-{timestamp}.zip
   ```

### HAR File

1. Download the `network-{timestamp}.har` file
2. Open in [HAR Viewer](https://toolbox.googleapps.com/apps/har_analyzer/) or browser DevTools

### DOM Snapshot

Open the `dom-snapshot-{timestamp}.html` file in a browser to see the page state at failure.

### Timeline

The `timeline-{timestamp}.json` file contains a structured timeline of all events with timestamps.

## API Reference

### FailureDiagnosticsHelper

Main class for capturing diagnostic artifacts.

#### Methods

- `CaptureScreenshotAsync(IPage, string?)` - Capture screenshot
- `CaptureDomSnapshotAsync(IPage, string?)` - Capture DOM snapshot
- `CaptureConsoleLogsAsync(IPage, string?)` - Capture console logs
- `CapturePageMetadataAsync(IPage, string?)` - Capture page metadata
- `CaptureApplicationLogs(string, string?)` - Capture application logs
- `CaptureTimeline()` - Capture test timeline
- `CaptureFailureAsync(IPage?, Exception?)` - Capture all artifacts on failure
- `AddTimelineEvent(string, string)` - Add timeline event
- `SetScenarioId(string)` - Set scenario ID
- `SetFixtureId(string)` - Set fixture ID

#### Properties

- `CorrelationId` - Unique correlation ID for the test run
- `ScenarioId` - Scenario ID for the test
- `FixtureId` - Fixture ID for the test

### PlaywrightDiagnosticsExtensions

Extension methods for Playwright-specific diagnostics.

#### Methods

- `StartTracingAsync(IPage, FailureDiagnosticsHelper)` - Start Playwright tracing
- `StopTracingAsync(IPage, FailureDiagnosticsHelper, string?)` - Stop and save trace
- `CaptureTraceAsync(IPage, FailureDiagnosticsHelper, string?)` - Capture trace
- `CaptureHarAsync(IPage, FailureDiagnosticsHelper, string?)` - Capture HAR file
- `CapturePlaywrightDiagnosticsAsync(IPage, FailureDiagnosticsHelper)` - Capture all Playwright artifacts

## Best Practices

1. **Always use try-catch**: Wrap test logic in try-catch and call `CaptureFailureAsync`
2. **Start tracing early**: Call `StartTracingAsync` at the beginning of the test
3. **Add timeline events**: Record key events for better debugging
4. **Use correlation IDs**: Include correlation IDs in external logs for tracing
5. **Review artifacts**: Check artifacts after failures to understand root cause
6. **Clean up old artifacts**: Periodically clean up old diagnostic files

## Troubleshooting

### Artifacts not captured

- Ensure `CaptureFailureAsync` is called in the catch block
- Check that the page is not null when passing to capture methods
- Verify write permissions to `~/.ghost/test-diagnostics/`

### Trace file empty

- Ensure `StartTracingAsync` is called before test actions
- Check that `StopTracingAsync` is called after failure
- Verify Playwright is properly installed

### HAR file missing

- HAR capture requires the page to have network activity
- Some pages may block HAR capture due to security policies

## Future Enhancements

- [ ] Video capture of test execution
- [ ] Memory and CPU profiling
- [ ] Network request/response bodies
- [ ] Custom artifact types
- [ ] Artifact compression and upload optimization
- [ ] Integration with external monitoring systems
