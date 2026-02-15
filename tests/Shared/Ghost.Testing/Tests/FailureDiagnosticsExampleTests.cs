using Ghost.Testing.Extensions;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Testing.Tests;

/// <summary>
/// Example test demonstrating the use of failure diagnostics capture.
/// Shows how to inherit from ReliabilityTestBase and capture diagnostics on failure.
/// </summary>
[Collection("Browser")]
public class FailureDiagnosticsExampleTests : ReliabilityTestBase
{
    private readonly RealBrowserFixture _fixture;

    public FailureDiagnosticsExampleTests(ITestOutputHelper output, RealBrowserFixture fixture)
        : base(output)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExampleTest_WithDiagnostics()
    {
        // Record timeline events for better debugging
        AddTimelineEvent("TestStarted", "Beginning example test");

        try
        {
            // Create a browser session
            IBrowserSession session = await _fixture.CreateSessionAsync().ConfigureAwait(false);
            AddTimelineEvent("SessionCreated", $"Session ID: {session.SessionId}");

            // Create a page
            IPage page = await session.NewPageAsync().ConfigureAwait(false);
            AddTimelineEvent("PageCreated", $"Page ID: {page.PageId}");

            // Start tracing for this test
            await page.StartTracingAsync(Diagnostics).ConfigureAwait(false);
            AddTimelineEvent("TracingStarted", "Playwright tracing enabled");

            // Navigate to a page
            await page.NavigateAsync("https://example.com").ConfigureAwait(false);
            AddTimelineEvent("NavigationComplete", $"Navigated to {page.Url}");

            // Perform some actions
            string title = await page.EvaluateAsync<string>("() => document.title").ConfigureAwait(false);
            AddTimelineEvent("TitleRetrieved", $"Page title: {title}");

            // Assert something
            Assert.NotNull(title);
            Assert.Contains("Example", title);

            AddTimelineEvent("TestPassed", "All assertions passed");
        }
        catch (Exception ex)
        {
            // Capture failure diagnostics automatically
            // This will capture: screenshot, DOM snapshot, console logs, page metadata, timeline, trace
            IBrowserSession session = await _fixture.CreateSessionAsync().ConfigureAwait(false);
            IPage page = await session.NewPageAsync().ConfigureAwait(false);

            await CaptureFailureAsync(page, ex).ConfigureAwait(false);

            // Re-throw the exception to fail the test
            throw;
        }
    }

    [Fact]
    public async Task ExampleTest_ManualDiagnosticsCapture()
    {
        AddTimelineEvent("ManualCaptureTest", "Starting manual diagnostics capture test");

        IBrowserSession session = await _fixture.CreateSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Start tracing
        await page.StartTracingAsync(Diagnostics).ConfigureAwait(false);

        try
        {
            await page.NavigateAsync("https://example.com").ConfigureAwait(false);

            // Simulate a failure condition
            IElement elementExists = await page.QuerySelectorAsync(".non-existent-element").ConfigureAwait(false) ?? throw new InvalidOperationException("Expected element not found");
        }
        catch (Exception ex)
        {
            // Manually capture specific artifacts
            await Diagnostics.CaptureScreenshotAsync(page).ConfigureAwait(false);
            await Diagnostics.CaptureDomSnapshotAsync(page).ConfigureAwait(false);
            await Diagnostics.CaptureConsoleLogsAsync(page).ConfigureAwait(false);
            await Diagnostics.CapturePageMetadataAsync(page).ConfigureAwait(false);

            // Capture Playwright-specific artifacts
            await page.StopTracingAsync(Diagnostics).ConfigureAwait(false);
            await page.CaptureHarAsync(Diagnostics).ConfigureAwait(false);

            // Capture the failure with all artifacts
            await CaptureFailureAsync(page, ex).ConfigureAwait(false);

            throw;
        }
    }

    [Fact]
    public async Task ExampleTest_WithoutBrowser()
    {
        AddTimelineEvent("NonBrowserTest", "Starting non-browser test");

        try
        {
            // Some logic that might fail
            int result = ComputeSomething();
            Assert.Equal(42, result);

            AddTimelineEvent("NonBrowserTestPassed", "Computation successful");
        }
        catch (Exception ex)
        {
            // Capture failure even without a browser page
            // This will capture: exception details, timeline, application logs
            await CaptureFailureAsync(null, ex).ConfigureAwait(false);

            throw;
        }
    }

    private static int ComputeSomething()
    {
        // Simulate a computation
        return 41; // This will fail the assertion
    }
}
