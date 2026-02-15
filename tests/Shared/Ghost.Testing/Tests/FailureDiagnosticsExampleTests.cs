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
            IBrowserSession session = await _fixture.CreateSessionAsync();
            AddTimelineEvent("SessionCreated", $"Session ID: {session.SessionId}");

            // Create a page
            IPage page = await session.NewPageAsync();
            AddTimelineEvent("PageCreated", $"Page ID: {page.PageId}");

            // Start tracing for this test
            await page.StartTracingAsync(Diagnostics);
            AddTimelineEvent("TracingStarted", "Playwright tracing enabled");

            // Navigate to a page
            await page.NavigateAsync("https://example.com");
            AddTimelineEvent("NavigationComplete", $"Navigated to {page.Url}");

            // Perform some actions
            string title = await page.EvaluateAsync<string>("() => document.title");
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
            IBrowserSession session = await _fixture.CreateSessionAsync();
            IPage page = await session.NewPageAsync();

            await CaptureFailureAsync(page, ex);

            // Re-throw the exception to fail the test
            throw;
        }
    }

    [Fact]
    public async Task ExampleTest_ManualDiagnosticsCapture()
    {
        AddTimelineEvent("ManualCaptureTest", "Starting manual diagnostics capture test");

        IBrowserSession session = await _fixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();

        // Start tracing
        await page.StartTracingAsync(Diagnostics);

        try
        {
            await page.NavigateAsync("https://example.com");

            // Simulate a failure condition
            IElement elementExists = await page.QuerySelectorAsync(".non-existent-element") ?? throw new InvalidOperationException("Expected element not found");
        }
        catch (Exception ex)
        {
            // Manually capture specific artifacts
            await Diagnostics.CaptureScreenshotAsync(page);
            await Diagnostics.CaptureDomSnapshotAsync(page);
            await Diagnostics.CaptureConsoleLogsAsync(page);
            await Diagnostics.CapturePageMetadataAsync(page);

            // Capture Playwright-specific artifacts
            await page.StopTracingAsync(Diagnostics);
            await page.CaptureHarAsync(Diagnostics);

            // Capture the failure with all artifacts
            await CaptureFailureAsync(page, ex);

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
            await CaptureFailureAsync(null, ex);

            throw;
        }
    }

    private static int ComputeSomething()
    {
        // Simulate a computation
        return 41; // This will fail the assertion
    }
}
