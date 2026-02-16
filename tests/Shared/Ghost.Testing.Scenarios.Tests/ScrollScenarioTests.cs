using System.Text.Json;
using FluentAssertions;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Microsoft.Extensions.Logging;
using Patchright;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Testing.Scenarios.Tests;

/// <summary>
/// Tests for infinite scroll scenarios.
/// Verifies completeness, deduplication, termination, and performance.
/// </summary>
[Collection("Browser Collection")]
public class ScrollScenarioTests : IAsyncLifetime
{
    private readonly BrowserFixture _browserFixture;
    private readonly ITestOutputHelper _output;
    private ScenarioServer? _scenarioServer;
    private string? _baseUrl;

    public ScrollScenarioTests(BrowserFixture browserFixture, ITestOutputHelper output)
    {
        _browserFixture = browserFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _scenarioServer = await ScenarioServer.CreateAsync();
        _baseUrl = _scenarioServer.BaseUrl;
        _output.WriteLine($"Scenario server started at {_baseUrl}");
    }

    public async Task DisposeAsync()
    {
        if (_scenarioServer != null)
        {
            await _scenarioServer.StopAsync();
            _scenarioServer.Dispose();
        }
    }

    [Fact]
    public async Task AutoThreshold_ShouldExtractAllJobs()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/auto-threshold";
        List<string> extractedJobs = [];

        // Listen for console logs to track loading
        List<string> consoleMessages = [];
        page.Console += (_, msg) =>
        {
            var text = msg.Text;
            consoleMessages.Add(text);
            _output.WriteLine($"[Console] {text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Scroll to trigger auto-loading until end
        var previousHeight = 0L;
        var stableCount = 0;
        var maxScrolls = 50; // Safety limit

        for (var i = 0; i < maxScrolls; i++)
        {
            // Scroll to bottom
            await page.EvaluateAsync<long>("() => document.documentElement.scrollHeight");
            await page.EvaluateAsync<long>("() => window.scrollTo(0, document.documentElement.scrollHeight)");

            // Wait for potential content load
            await Task.Delay(300);

            var currentHeight = await page.EvaluateAsync<long>("() => document.documentElement.scrollHeight");

            if (currentHeight == previousHeight)
            {
                stableCount++;
                if (stableCount >= 3) break; // No more content loading
            }
            else
            {
                stableCount = 0;
                previousHeight = currentHeight;
            }
        }

        // Extract all job IDs
        extractedJobs = (await page.Locator(".job[data-job-id]").AllAsync())
            .Select(async el => await el.GetAttributeAsync("data-job-id"))
            .Select(id => id!)
            .ToList();

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Should().HaveCountGreaterOrEqualTo(100, "should load multiple pages via auto-scroll");

        // Verify no duplicates
        var uniqueJobs = extractedJobs.Distinct().ToList();
        uniqueJobs.Should().HaveCount(extractedJobs.Count, "should have no duplicate job IDs");

        // Verify sequential IDs
        var numericIds = extractedJobs
            .Select(id => int.Parse(id.Replace("job-", "")))
            .OrderBy(x => x)
            .ToList();

        numericIds.Should().BeInAscendingOrder();
        numericIds.First().Should().Be(0);
        numericIds.Last().Should().BeGreaterOrEqualTo(99);

        _output.WriteLine($"Extracted {extractedJobs.Count} unique jobs from auto-threshold scroll");
    }

    [Fact]
    public async Task ButtonDriven_ShouldExtractAllJobs()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/button-driven";
        List<string> extractedJobs = [];

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var loadMoreBtn = page.Locator("#load-more-btn");
        var clickCount = 0;
        var maxClicks = 50; // Safety limit

        while (await loadMoreBtn.IsEnabledAsync() && clickCount < maxClicks)
        {
            await loadMoreBtn.ClickAsync();
            clickCount++;
            await Task.Delay(200); // Wait for content to load
        }

        // Extract all job IDs
        extractedJobs = (await page.Locator(".job[data-job-id]").AllAsync())
            .Select(async el => await el.GetAttributeAsync("data-job-id"))
            .Select(id => id!)
            .ToList();

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Should().HaveCountGreaterOrEqualTo(100, "should load multiple pages via button clicks");

        // Verify no duplicates
        var uniqueJobs = extractedJobs.Distinct().ToList();
        uniqueJobs.Should().HaveCount(extractedJobs.Count, "should have no duplicate job IDs");

        // Verify button state at end
        var btnText = await loadMoreBtn.TextContentAsync();
        btnText.Should().Be("No More Jobs", "button should show end state");

        _output.WriteLine($"Extracted {extractedJobs.Count} unique jobs after {clickCount} button clicks");
    }

    [Fact]
    public async Task Virtualized_ShouldHandleLargeList()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/virtualized";
        List<string> extractedJobs = [];

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var viewport = page.Locator("#viewport");

        // Scroll through the virtualized list
        for (var i = 0; i < 20; i++)
        {
            await viewport.EvaluateAsync<long>("el => el.scrollTop += 300");
            await Task.Delay(100);

            // Extract current visible jobs
            var visibleJobs = await page.Locator(".job[data-job-id]").AllAsync();
            var currentIds = visibleJobs
                .Select(async el => await el.GetAttributeAsync("data-job-id"))
                .Select(id => id!)
                .ToList();

            extractedJobs.AddRange(currentIds);
        }

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");

        // In virtualized list, we should see different items as we scroll
        var uniqueJobs = extractedJobs.Distinct().ToList();
        uniqueJobs.Should().HaveCountGreaterOrEqualTo(20, "should see multiple different items through virtualization");

        // Verify job IDs follow expected pattern
        foreach (var jobId in uniqueJobs.Take(10))
        {
            jobId.Should().Match("job-*", "job IDs should follow expected pattern");
        }

        _output.WriteLine($"Extracted {uniqueJobs.Count} unique jobs from virtualized scroll");
    }

    [Fact]
    public async Task DuplicateChunk_ShouldHandleDeduplication()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/duplicate-chunk";
        List<string> extractedJobs = [];

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var loadMoreBtn = page.Locator("#load-more-btn");
        var clickCount = 0;
        var maxClicks = 20;

        while (await loadMoreBtn.IsEnabledAsync() && clickCount < maxClicks)
        {
            await loadMoreBtn.ClickAsync();
            clickCount++;
            await Task.Delay(200);
        }

        // Extract all job IDs
        extractedJobs = (await page.Locator(".job[data-job-id]").AllAsync())
            .Select(async el => await el.GetAttributeAsync("data-job-id"))
            .Select(id => id!)
            .ToList();

        // Extract stats from the page
        var totalCount = int.Parse(await page.Locator("#total-count").TextContentAsync() ?? "0");
        var uniqueCount = int.Parse(await page.Locator("#unique-count").TextContentAsync() ?? "0");
        var duplicateCount = int.Parse(await page.Locator("#duplicate-count").TextContentAsync() ?? "0");

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Count.Should().Be(totalCount, "extracted count should match page total");

        // Verify duplicates were detected
        duplicateCount.Should().BeGreaterThan(0, "should detect duplicate chunks");
        uniqueCount.Should().Be(totalCount - duplicateCount, "unique count should be total minus duplicates");

        // Verify unique jobs are actually unique
        var uniqueJobs = extractedJobs.Distinct().ToList();
        uniqueJobs.Should().HaveCount(uniqueCount, "unique job count should match page unique count");

        // Verify duplicates are marked visually
        var duplicateElements = await page.Locator(".job.duplicate").CountAsync();
        duplicateElements.Should().Be(duplicateCount, "duplicate elements should be marked");

        _output.WriteLine($"Total: {totalCount}, Unique: {uniqueCount}, Duplicates: {duplicateCount}");
    }

    [Fact]
    public async Task AutoThreshold_ShouldTerminateAtEnd()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/auto-threshold";

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Scroll to end
        var previousHeight = 0L;
        var stableCount = 0;

        for (var i = 0; i < 30; i++)
        {
            await page.EvaluateAsync<long>("() => window.scrollTo(0, document.documentElement.scrollHeight)");
            await Task.Delay(300);

            var currentHeight = await page.EvaluateAsync<long>("() => document.documentElement.scrollHeight");

            if (currentHeight == previousHeight)
            {
                stableCount++;
                if (stableCount >= 5) break;
            }
            else
            {
                stableCount = 0;
                previousHeight = currentHeight;
            }
        }

        // Assert - verify loading indicator is hidden
        var loadingVisible = await page.Locator("#loading").IsVisibleAsync();
        loadingVisible.Should().BeFalse("loading indicator should be hidden at end");

        // Verify we can't scroll further
        var scrollTop = await page.EvaluateAsync<long>("() => window.scrollY");
        var scrollHeight = await page.EvaluateAsync<long>("() => document.documentElement.scrollHeight");
        var clientHeight = await page.EvaluateAsync<long>("() => window.innerHeight");

        (scrollTop + clientHeight).Should().BeGreaterOrEqualTo(scrollHeight - 10, "should be at or near bottom");
    }

    [Fact]
    public async Task ButtonDriven_ShouldHandleLoadingState()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/scroll/button-driven";

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var loadMoreBtn = page.Locator("#load-more-btn");

        // Click button and check loading state
        var clickTask = loadMoreBtn.ClickAsync();

        // Check button is disabled during load
        var isDisabled = await loadMoreBtn.IsDisabledAsync();
        isDisabled.Should().BeTrue("button should be disabled during loading");

        var btnText = await loadMoreBtn.TextContentAsync();
        btnText.Should().Be("Loading...", "button should show loading text");

        await clickTask;

        // Wait for load to complete
        await Task.Delay(300);

        // Check button is enabled again
        isDisabled = await loadMoreBtn.IsDisabledAsync();
        btnText = await loadMoreBtn.TextContentAsync();

        // After first load, button should be enabled (unless at end)
        if (await loadMoreBtn.IsEnabledAsync())
        {
            btnText.Should().Be("Load More Jobs", "button should show normal text after load");
        }
    }

    [Fact]
    public async Task AllScenarios_ShouldBeAccessible()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var scenarios = new[]
        {
            "/scenario/scroll/auto-threshold",
            "/scenario/scroll/button-driven",
            "/scenario/scroll/virtualized",
            "/scenario/scroll/duplicate-chunk"
        };

        // Act & Assert
        foreach (var scenario in scenarios)
        {
            var url = $"{_baseUrl}{scenario}";
            var response = await page.GotoAsync(url);
            response.Should().NotBeNull($"scenario {scenario} should be accessible");
            response!.Status.Should().Be(200, $"scenario {scenario} should return 200");

            var title = await page.TitleAsync();
            title.Should().Contain("Jobs", $"scenario {scenario} should have proper title");

            _output.WriteLine($"✓ {scenario} accessible");
        }
    }
}
