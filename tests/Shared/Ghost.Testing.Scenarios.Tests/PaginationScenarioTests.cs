using FluentAssertions;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Patchright;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Testing.Scenarios.Tests;

/// <summary>
/// Tests for pagination scenarios.
/// Verifies completeness, deduplication, termination, loop detection, and cursor integrity.
/// </summary>
[Collection("Browser Collection")]
public class PaginationScenarioTests : IAsyncLifetime
{
    private readonly BrowserFixture _browserFixture;
    private readonly ITestOutputHelper _output;
    private ScenarioServer? _scenarioServer;
    private string? _baseUrl;

    public PaginationScenarioTests(BrowserFixture browserFixture, ITestOutputHelper output)
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
    public async Task Numbered_ShouldExtractAllJobs()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/numbered";
        var extractedJobs = new List<string>();

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate through all pages
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 50; // Safety limit

        while (hasNext && pageCount < maxPages)
        {
            // Extract job IDs from current page
            var currentJobs = await page.Locator(".job[data-job-id]").AllAsync();
            var currentIds = currentJobs
                .Select(async el => await el.GetAttributeAsync("data-job-id"))
                .Select(id => id!)
                .ToList();

            extractedJobs.AddRange(currentIds);
            pageCount++;

            // Try to find and click next link
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Should().HaveCountGreaterOrEqualTo(100, "should navigate through multiple pages");

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

        _output.WriteLine($"Extracted {extractedJobs.Count} unique jobs from {pageCount} pages");
    }

    [Fact]
    public async Task Cursor_ShouldExtractAllJobs()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/cursor";
        var extractedJobs = new List<string>();

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate through all pages using cursor
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 50; // Safety limit

        while (hasNext && pageCount < maxPages)
        {
            // Extract job IDs from current page
            var currentJobs = await page.Locator(".job[data-job-id]").AllAsync();
            var currentIds = currentJobs
                .Select(async el => await el.GetAttributeAsync("data-job-id"))
                .Select(id => id!)
                .ToList();

            extractedJobs.AddRange(currentIds);
            pageCount++;

            // Try to find and click next link
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Load Next Page" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Should().HaveCountGreaterOrEqualTo(100, "should navigate through multiple pages");

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

        _output.WriteLine($"Extracted {extractedJobs.Count} unique jobs from {pageCount} cursor pages");
    }

    [Fact]
    public async Task JumpToPage_ShouldNavigateDirectly()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/jump-to-page";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Jump to page 5 directly
        var pageInput = page.Locator("#jumpPage");
        await pageInput.FillAsync("5");

        var goButton = page.Locator("button").Filter(new() { HasText = "Go" });
        await goButton.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Extract job IDs from page 5
        var currentJobs = await page.Locator(".job[data-job-id]").AllAsync();
        var extractedJobs = currentJobs
            .Select(async el => await el.GetAttributeAsync("data-job-id"))
            .Select(id => id!)
            .ToList();

        // Assert
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        extractedJobs.Should().HaveCount(10, "should have 10 jobs per page");

        // Verify we're on page 5 (jobs should start around index 40)
        var numericIds = extractedJobs
            .Select(id => int.Parse(id.Replace("job-", "")))
            .OrderBy(x => x)
            .ToList();

        numericIds.First().Should().Be(40, "page 5 should start with job-40");
        numericIds.Last().Should().Be(49, "page 5 should end with job-49");

        _output.WriteLine($"Successfully jumped to page 5, extracted {extractedJobs.Count} jobs");
    }

    [Fact]
    public async Task LastPageDetection_ShouldIdentifyEnd()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/last-page-detection";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate to last page
        var hasNext = true;
        while (hasNext)
        {
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        // Assert - verify end marker is present
        var endMarker = page.Locator("[data-end-marker]");
        var endMarkerVisible = await endMarker.CountAsync() > 0;
        endMarkerVisible.Should().BeTrue("end marker should be visible on last page");

        var endMarkerText = await endMarker.TextContentAsync();
        endMarkerText.Should().Contain("last page", "end marker should indicate last page");

        // Verify next link is disabled
        var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
        var nextLinkDisabled = await nextLink.EvaluateAsync<bool>("el => el.classList.contains('disabled')");
        nextLinkDisabled.Should().BeTrue("next link should be disabled on last page");

        _output.WriteLine("Last page detection working correctly");
    }

    [Fact]
    public async Task TokenExpiration_ShouldHandleExpiredTokens()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/token-expiration";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate through pages until token expires (after page 3)
        var hasNext = true;
        var pageCount = 0;

        while (hasNext && pageCount < 5)
        {
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Load Next Page" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                pageCount++;
            }
        }

        // Assert - verify error message is shown
        var errorElement = page.Locator("[data-error-type='token-expired']");
        var errorVisible = await errorElement.CountAsync() > 0;
        errorVisible.Should().BeTrue("token expiration error should be shown");

        var errorText = await errorElement.TextContentAsync();
        errorText.Should().Contain("expired", "error should mention token expiration");

        // Verify retry button exists
        var retryButton = page.Locator("button").Filter(new() { HasText = "Start from beginning" });
        var retryVisible = await retryButton.CountAsync() > 0;
        retryVisible.Should().BeTrue("retry button should be available");

        _output.WriteLine($"Token expiration handled correctly after {pageCount} pages");
    }

    [Fact]
    public async Task EmptyPage_ShouldTerminateGracefully()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/empty-page";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate until empty page
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 60; // Go beyond total jobs to trigger empty page

        while (hasNext && pageCount < maxPages)
        {
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Load Next Page" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                pageCount++;
            }
        }

        // Assert - verify empty state is shown
        var emptyState = page.Locator("[data-empty-state]");
        var emptyStateVisible = await emptyState.CountAsync() > 0;
        emptyStateVisible.Should().BeTrue("empty state should be shown when no more results");

        var emptyText = await emptyState.TextContentAsync();
        emptyText.Should().Contain("No more results", "empty state should indicate end of results");

        _output.WriteLine($"Empty page termination working correctly after {pageCount} pages");
    }

    [Fact]
    public async Task DynamicUrl_ShouldUpdateHistory()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/dynamic-url";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Get initial URL
        var initialUrl = page.Url;
        initialUrl.Should().Contain("page=1", "initial URL should have page=1");

        // Click next button
        var nextButton = page.Locator("#nextBtn");
        await nextButton.ClickAsync();
        await Task.Delay(100); // Wait for URL update

        // Assert - verify URL was updated
        var updatedUrl = page.Url;
        updatedUrl.Should().Contain("page=2", "URL should be updated to page=2");
        updatedUrl.Should().NotBe(initialUrl, "URL should have changed");

        // Verify URL display is updated
        var urlDisplay = page.Locator("#currentUrl");
        var displayText = await urlDisplay.TextContentAsync();
        displayText.Should().Contain("page=2", "URL display should show updated page");

        _output.WriteLine($"Dynamic URL update working: {initialUrl} -> {updatedUrl}");
    }

    [Fact]
    public async Task CircularPagination_ShouldDetectLoop()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/circular";
        var visitedPages = new HashSet<string>();

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate and detect loop
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 10; // Should detect loop before this

        while (hasNext && pageCount < maxPages)
        {
            // Track current page
            var currentUrl = page.Url;
            visitedPages.Add(currentUrl);

            // Try to find next link
            var nextLink = page.Locator(".pagination a");
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                pageCount++;

                // Check if we've seen this URL before (loop detection)
                if (visitedPages.Contains(page.Url))
                {
                    _output.WriteLine($"Loop detected at page {pageCount}");
                    break;
                }
            }
        }

        // Assert - verify warning is present
        var warning = page.Locator("[data-warning-type='circular-pagination']");
        var warningVisible = await warning.CountAsync() > 0;
        warningVisible.Should().BeTrue("circular pagination warning should be shown");

        // Verify we detected the loop (should not reach maxPages)
        pageCount.Should().BeLessThan(maxPages, "should detect loop before reaching max pages");

        _output.WriteLine($"Circular pagination detected after {pageCount} pages");
    }

    [Fact]
    public async Task MissingNextLink_ShouldHandleDeadEnd()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/missing-next-link";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate to page where next link is missing (page 3)
        var hasNext = true;
        var pageCount = 0;

        while (hasNext && pageCount < 5)
        {
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                pageCount++;
            }
        }

        // Assert - verify warning is present
        var warning = page.Locator("[data-warning-type='missing-next-link']");
        var warningVisible = await warning.CountAsync() > 0;
        warningVisible.Should().BeTrue("missing next link warning should be shown");

        // Verify next link is not available
        var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
        var nextLinkCount = await nextLink.CountAsync();
        nextLinkCount.Should().Be(0, "next link should not be available");

        _output.WriteLine($"Missing next link handled correctly at page {pageCount}");
    }

    [Fact]
    public async Task InfiniteRedirect_ShouldPreventLoop()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/infinite-redirect";

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });

        // Wait for redirect loop to complete (should stop after 10 redirects)
        await Task.Delay(2000);

        // Assert - verify warning is present
        var warning = page.Locator("[data-warning-type='infinite-redirect']");
        var warningVisible = await warning.CountAsync() > 0;
        warningVisible.Should().BeTrue("infinite redirect warning should be shown");

        // Verify redirect count is tracked
        var warningText = await warning.TextContentAsync();
        warningText.Should().Contain("10", "should show redirect count");

        // Verify page is displayed (not stuck in redirect)
        var jobs = await page.Locator(".job[data-job-id]").CountAsync();
        jobs.Should().BeGreaterThan(0, "page should be displayed after redirect limit");

        _output.WriteLine("Infinite redirect protection working correctly");
    }

    [Fact]
    public async Task SafeTermination_ShouldCompleteGracefully()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/safe-termination";
        var extractedJobs = new List<string>();

        // Listen for console logs
        page.Console += (_, msg) =>
        {
            _output.WriteLine($"[Console] {msg.Text}");
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate to end
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 60;

        while (hasNext && pageCount < maxPages)
        {
            // Extract job IDs
            var currentJobs = await page.Locator(".job[data-job-id]").AllAsync();
            var currentIds = currentJobs
                .Select(async el => await el.GetAttributeAsync("data-job-id"))
                .Select(id => id!)
                .ToList();

            extractedJobs.AddRange(currentIds);
            pageCount++;

            // Try to find next link
            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        // Assert - verify termination marker is present
        var terminationMarker = page.Locator("[data-termination-marker]");
        var terminationVisible = await terminationMarker.CountAsync() > 0;
        terminationVisible.Should().BeTrue("termination marker should be shown");

        var terminationText = await terminationMarker.TextContentAsync();
        terminationText.Should().Contain("Safe Termination", "should indicate safe termination");
        terminationText.Should().Contain("500", "should show total job count");

        // Verify all jobs were extracted without duplicates
        extractedJobs.Should().NotBeEmpty("jobs should be extracted");
        var uniqueJobs = extractedJobs.Distinct().ToList();
        uniqueJobs.Should().HaveCount(extractedJobs.Count, "should have no duplicates");

        _output.WriteLine($"Safe termination: {extractedJobs.Count} unique jobs from {pageCount} pages");
    }

    [Fact]
    public async Task AllScenarios_ShouldBeAccessible()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var scenarios = new[]
        {
            "/scenario/pagination/numbered",
            "/scenario/pagination/cursor",
            "/scenario/pagination/mixed",
            "/scenario/pagination/jump-to-page",
            "/scenario/pagination/last-page-detection",
            "/scenario/pagination/token-expiration",
            "/scenario/pagination/empty-page",
            "/scenario/pagination/dynamic-url",
            "/scenario/pagination/circular",
            "/scenario/pagination/missing-next-link",
            "/scenario/pagination/infinite-redirect",
            "/scenario/pagination/safe-termination"
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

    [Fact]
    public async Task Numbered_ShouldNotSkipItems()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/numbered";
        var extractedJobs = new List<string>();

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate through all pages
        var hasNext = true;
        var maxPages = 50;

        while (hasNext && extractedJobs.Count < maxPages * 10)
        {
            var currentJobs = await page.Locator(".job[data-job-id]").AllAsync();
            var currentIds = currentJobs
                .Select(async el => await el.GetAttributeAsync("data-job-id"))
                .Select(id => id!)
                .ToList();

            extractedJobs.AddRange(currentIds);

            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Next" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        // Assert - verify no gaps in sequential IDs
        var numericIds = extractedJobs
            .Select(id => int.Parse(id.Replace("job-", "")))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        for (var i = 0; i < numericIds.Count - 1; i++)
        {
            (numericIds[i + 1] - numericIds[i]).Should().Be(1, $"should have no gaps between job-{numericIds[i]} and job-{numericIds[i + 1]}");
        }

        _output.WriteLine($"Verified no skips in {numericIds.Count} sequential jobs");
    }

    [Fact]
    public async Task Cursor_ShouldMaintainTokenIntegrity()
    {
        // Arrange
        using var page = await _browserFixture.Browser.NewPageAsync();
        var url = $"{_baseUrl}/scenario/pagination/cursor";
        var cursors = new List<string>();

        // Listen for console logs to track cursors
        page.Console += (_, msg) =>
        {
            var text = msg.Text;
            if (text.Contains("cursor="))
            {
                _output.WriteLine($"[Console] {text}");
            }
        };

        // Act
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Navigate through pages and track cursors
        var hasNext = true;
        var pageCount = 0;
        var maxPages = 10;

        while (hasNext && pageCount < maxPages)
        {
            // Extract current cursor from URL
            var currentUrl = page.Url;
            var cursorMatch = System.Text.RegularExpressions.Regex.Match(currentUrl, "cursor=([^&]+)");
            if (cursorMatch.Success)
            {
                cursors.Add(cursorMatch.Groups[1].Value);
            }

            var nextLink = page.Locator(".pagination a").Filter(new() { HasText = "Load Next Page" });
            hasNext = await nextLink.CountAsync() > 0;

            if (hasNext)
            {
                await nextLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                pageCount++;
            }
        }

        // Assert - verify cursors are unique and sequential
        cursors.Should().HaveCount(pageCount, "should have one cursor per page");
        cursors.Distinct().Should().HaveCount(cursors.Count, "cursors should be unique");

        // Verify cursor progression (each should be different)
        for (var i = 0; i < cursors.Count - 1; i++)
        {
            cursors[i].Should().NotBe(cursors[i + 1], $"cursor {i} should differ from cursor {i + 1}");
        }

        _output.WriteLine($"Cursor integrity verified: {cursors.Count} unique cursors");
    }
}
