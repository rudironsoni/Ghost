using System.Globalization;
using Ghost.Kernel;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Scroll;

/// <summary>
/// Integration tests for infinite scroll and virtualization scenarios using the synthetic scenario server.
/// Tests various scroll-loading patterns: auto-fetch on threshold, button-driven loads,
/// virtualized DOM replacements, and duplicate chunk replay.
/// </summary>
[Collection("Browser")]
[Trait("Category", "End2End")]
[Trait("Capability", "RequiresSyntheticServer")]
public class ScrollScenarioTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RealBrowserFixture _browserFixture;
    private ScenarioServer? _scenarioServer;

    public ScrollScenarioTests(ITestOutputHelper output, RealBrowserFixture browserFixture)
    {
        _output = output;
        _browserFixture = browserFixture;
    }

    public async Task InitializeAsync()
    {
        _scenarioServer = await ScenarioServer.CreateAsync().ConfigureAwait(false);
        _output.WriteLine($"Scenario server started at {_scenarioServer.BaseUrl}");
    }

    public async Task DisposeAsync()
    {
        if (_scenarioServer != null)
        {
            await _scenarioServer.StopAsync().ConfigureAwait(false);
            _scenarioServer.Dispose();
        }
    }

    [Fact]
    public async Task AutoThreshold_ScrollTriggersFetch_LoadsMoreItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/auto-threshold";

        // Act
        await page.NavigateAsync(url);

        // Verify initial items are loaded
        IReadOnlyList<IElement> initialJobs = await page.QuerySelectorAllAsync(".job");
        Assert.True(initialJobs.Count >= 15, $"Expected at least 15 initial jobs, got {initialJobs.Count}");
        _output.WriteLine($"Initial jobs loaded: {initialJobs.Count}");

        // Scroll to trigger auto-fetch
        await page.EvaluateAsync<string>("window.scrollTo(0, document.body.scrollHeight)");
        await Task.Delay(500); // Wait for fetch to complete

        // Assert - More items should be loaded
        IReadOnlyList<IElement> jobsAfterScroll = await page.QuerySelectorAllAsync(".job");
        Assert.True(jobsAfterScroll.Count > initialJobs.Count,
            $"Expected more jobs after scroll. Initial: {initialJobs.Count}, After: {jobsAfterScroll.Count}");

        _output.WriteLine($"Jobs after scroll: {jobsAfterScroll.Count}");
    }

    [Fact]
    public async Task AutoThreshold_MultipleScrolls_LoadsAllItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/auto-threshold";

        // Act
        await page.NavigateAsync(url);

        int previousCount = 0;
        int scrollCount = 0;
        int maxScrolls = 10; // Prevent infinite loop

        // Scroll until no more items are loaded
        while (scrollCount < maxScrolls)
        {
            await page.EvaluateAsync<string>("window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(500);

            IReadOnlyList<IElement> currentJobs = await page.QuerySelectorAllAsync(".job");
            int currentCount = currentJobs.Count;

            _output.WriteLine($"Scroll {scrollCount + 1}: {currentCount} jobs");

            if (currentCount == previousCount)
            {
                // No more items loaded, we've reached the end
                break;
            }

            previousCount = currentCount;
            scrollCount++;
        }

        // Assert - Completeness: All items should be extracted
        IReadOnlyList<IElement> finalJobs = await page.QuerySelectorAllAsync(".job");
        Assert.True(finalJobs.Count >= 100, $"Expected at least 100 jobs, got {finalJobs.Count}");

        // Assert - Dedupe: No duplicates
        HashSet<string> jobIds = [];
        foreach (IElement job in finalJobs)
        {
            string? jobId = await job.GetAttributeAsync("data-job-id");
            Assert.NotNull(jobId);
            Assert.False(jobIds.Contains(jobId), $"Duplicate job ID found: {jobId}");
            jobIds.Add(jobId);
        }

        _output.WriteLine($"Total unique jobs loaded: {jobIds.Count}");
    }

    [Fact]
    public async Task AutoThreshold_Termination_StopsAtEnd()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/auto-threshold";

        // Act
        await page.NavigateAsync(url);

        // Scroll to the end
        int previousCount = 0;
        int stableCount = 0;

        for (int i = 0; i < 20; i++)
        {
            await page.EvaluateAsync<string>("window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(500);

            IReadOnlyList<IElement> currentJobs = await page.QuerySelectorAllAsync(".job");
            int currentCount = currentJobs.Count;

            if (currentCount == previousCount)
            {
                stableCount++;
                if (stableCount >= 3)
                {
                    // Count has been stable for 3 scrolls, we've reached the end
                    break;
                }
            }
            else
            {
                stableCount = 0;
            }

            previousCount = currentCount;
        }

        // Assert - Termination: Loading indicator should be hidden
        IElement? loadingIndicator = await page.QuerySelectorAsync("#loading");
        Assert.NotNull(loadingIndicator);

        string? loadingStyle = await loadingIndicator.GetAttributeAsync("style");
        Assert.Contains("display: none;", loadingStyle ?? "");

        _output.WriteLine("Auto-threshold termination test passed");
    }

    [Fact]
    public async Task ButtonDriven_ClickLoadMore_LoadsMoreItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/button-driven";

        // Act
        await page.NavigateAsync(url);

        // Verify initial items are loaded
        IReadOnlyList<IElement> initialJobs = await page.QuerySelectorAllAsync(".job");
        Assert.True(initialJobs.Count >= 10, $"Expected at least 10 initial jobs, got {initialJobs.Count}");
        _output.WriteLine($"Initial jobs loaded: {initialJobs.Count}");

        // Click "Load More" button
        IElement? loadMoreButton = await page.QuerySelectorAsync("#load-more-btn");
        Assert.NotNull(loadMoreButton);

        await loadMoreButton.ClickAsync();
        await Task.Delay(500); // Wait for fetch to complete

        // Assert - More items should be loaded
        IReadOnlyList<IElement> jobsAfterClick = await page.QuerySelectorAllAsync(".job");
        Assert.True(jobsAfterClick.Count > initialJobs.Count,
            $"Expected more jobs after click. Initial: {initialJobs.Count}, After: {jobsAfterClick.Count}");

        _output.WriteLine($"Jobs after click: {jobsAfterClick.Count}");
    }

    [Fact]
    public async Task ButtonDriven_MultipleClicks_LoadsAllItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/button-driven";

        // Act
        await page.NavigateAsync(url);

        int clickCount = 0;
        int maxClicks = 20;

        // Click "Load More" until button is disabled or text changes
        while (clickCount < maxClicks)
        {
            IElement? loadMoreBtn = await page.QuerySelectorAsync("#load-more-btn");
            if (loadMoreBtn == null)
            {
                break;
            }

            string? btnText = await loadMoreBtn.GetTextContentAsync();
            if (btnText?.Contains("No More") == true)
            {
                break;
            }

            // Check if button is disabled by checking the disabled attribute
            string? isDisabledAttr = await loadMoreBtn.GetAttributeAsync("disabled");
            if (isDisabledAttr != null)
            {
                await Task.Delay(500); // Wait for loading to complete
                continue;
            }

            await loadMoreBtn.ClickAsync();
            await Task.Delay(500);
            clickCount++;

            IReadOnlyList<IElement> currentJobs = await page.QuerySelectorAllAsync(".job");
            _output.WriteLine($"Click {clickCount}: {currentJobs.Count} jobs");
        }

        // Assert - Completeness: All items should be extracted
        IReadOnlyList<IElement> finalJobs = await page.QuerySelectorAllAsync(".job");
        Assert.True(finalJobs.Count >= 100, $"Expected at least 100 jobs, got {finalJobs.Count}");

        // Assert - Dedupe: No duplicates
        HashSet<string> jobIds = [];
        foreach (IElement job in finalJobs)
        {
            string? jobId = await job.GetAttributeAsync("data-job-id");
            Assert.NotNull(jobId);
            Assert.False(jobIds.Contains(jobId), $"Duplicate job ID found: {jobId}");
            jobIds.Add(jobId);
        }

        _output.WriteLine($"Total unique jobs loaded: {jobIds.Count}");
    }

    [Fact]
    public async Task ButtonDriven_Termination_DisablesButtonAtEnd()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/button-driven";

        // Act
        await page.NavigateAsync(url);

        // Click until end
        for (int i = 0; i < 50; i++)
        {
            IElement? loadMoreBtn = await page.QuerySelectorAsync("#load-more-btn");
            if (loadMoreBtn == null)
            {
                break;
            }

            string? btnText = await loadMoreBtn.GetTextContentAsync();
            if (btnText?.Contains("No More") == true)
            {
                break;
            }

            // Check if button is disabled (loading state)
            string? isDisabledAttr = await loadMoreBtn.GetAttributeAsync("disabled");
            if (isDisabledAttr != null)
            {
                await Task.Delay(500); // Wait for loading to complete
                continue;
            }

            await loadMoreBtn.ClickAsync();
            await Task.Delay(500);
        }

        // Assert - Termination: Button should show "No More Jobs"
        IElement? finalBtn = await page.QuerySelectorAsync("#load-more-btn");
        Assert.NotNull(finalBtn);

        string? finalBtnText = await finalBtn.GetTextContentAsync();
        Assert.Contains("No More", finalBtnText ?? "");

        _output.WriteLine("Button-driven termination test passed");
    }

    [Fact]
    public async Task Virtualized_ScrollUpdatesDOM_ReplacesItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/virtualized";

        // Act
        await page.NavigateAsync(url);

        // Get initial visible items
        IReadOnlyList<IElement> initialJobs = await page.QuerySelectorAllAsync(".job");
        List<string> initialJobIds = [];

        foreach (IElement job in initialJobs)
        {
            string? jobId = await job.GetAttributeAsync("data-job-id");
            if (jobId != null)
            {
                initialJobIds.Add(jobId);
            }
        }

        _output.WriteLine($"Initial visible jobs: {initialJobIds.Count}");

        // Scroll down
        await page.EvaluateAsync<string>("document.getElementById('viewport').scrollTop = 500");
        await Task.Delay(300);

        // Get items after scroll
        IReadOnlyList<IElement> jobsAfterScroll = await page.QuerySelectorAllAsync(".job");
        List<string> afterScrollJobIds = [];

        foreach (IElement job in jobsAfterScroll)
        {
            string? jobId = await job.GetAttributeAsync("data-job-id");
            if (jobId != null)
            {
                afterScrollJobIds.Add(jobId);
            }
        }

        _output.WriteLine($"Jobs after scroll: {afterScrollJobIds.Count}");

        // Assert - Virtualized DOM replacements: Items should be different
        var commonIds = initialJobIds.Intersect(afterScrollJobIds).ToList();
        Assert.True(commonIds.Count < initialJobIds.Count,
            $"Expected different items after scroll. Common: {commonIds.Count}, Initial: {initialJobIds.Count}");

        _output.WriteLine($"Common items: {commonIds.Count}, New items: {afterScrollJobIds.Count - commonIds.Count}");
    }

    [Fact]
    public async Task Virtualized_MultipleScrolls_CoversAllRange()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/virtualized";

        // Act
        await page.NavigateAsync(url);

        HashSet<string> allSeenJobIds = [];
        int[] scrollPositions = new[] { 0, 500, 1000, 2000, 4000, 6000, 8000 };

        foreach (int scrollPos in scrollPositions)
        {
            await page.EvaluateAsync<string>($"document.getElementById('viewport').scrollTop = {scrollPos}");
            await Task.Delay(300);

            IReadOnlyList<IElement> jobs = await page.QuerySelectorAllAsync(".job");
            foreach (IElement job in jobs)
            {
                string? jobId = await job.GetAttributeAsync("data-job-id");
                if (jobId != null)
                {
                    allSeenJobIds.Add(jobId);
                }
            }

            _output.WriteLine($"Scroll position {scrollPos}: {jobs.Count} visible jobs, {allSeenJobIds.Count} unique seen");
        }

        // Assert - Completeness: Should see many different items across scrolls
        Assert.True(allSeenJobIds.Count >= 50, $"Expected at least 50 unique jobs across scrolls, got {allSeenJobIds.Count}");

        _output.WriteLine($"Total unique jobs seen across all scrolls: {allSeenJobIds.Count}");
    }

    [Fact]
    public async Task Virtualized_Dedupe_NoDuplicatesInViewport()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/virtualized";

        // Act
        await page.NavigateAsync(url);

        // Check multiple scroll positions for duplicates
        int[] scrollPositions = new[] { 0, 500, 1000, 2000, 4000 };

        foreach (int scrollPos in scrollPositions)
        {
            await page.EvaluateAsync<string>($"document.getElementById('viewport').scrollTop = {scrollPos}");
            await Task.Delay(300);

            IReadOnlyList<IElement> jobs = await page.QuerySelectorAllAsync(".job");
            HashSet<string> jobIds = [];

            foreach (IElement job in jobs)
            {
                string? jobId = await job.GetAttributeAsync("data-job-id");
                Assert.NotNull(jobId);
                Assert.False(jobIds.Contains(jobId), $"Duplicate job ID found at scroll {scrollPos}: {jobId}");
                jobIds.Add(jobId);
            }

            _output.WriteLine($"Scroll position {scrollPos}: {jobIds.Count} unique jobs (no duplicates)");
        }

        _output.WriteLine("Virtualized dedupe test passed");
    }

    [Fact]
    public async Task DuplicateChunkReplay_LoadsChunks_DetectsDuplicates()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/duplicate-chunk";

        // Act
        await page.NavigateAsync(url);

        // Get initial stats
        string initialTotal = await page.EvaluateAsync<string>("document.getElementById('total-count').textContent");
        string initialUnique = await page.EvaluateAsync<string>("document.getElementById('unique-count').textContent");
        string initialDuplicates = await page.EvaluateAsync<string>("document.getElementById('duplicate-count').textContent");

        _output.WriteLine($"Initial - Total: {initialTotal}, Unique: {initialUnique}, Duplicates: {initialDuplicates}");

        // Click "Load More" multiple times to trigger duplicate chunks
        for (int i = 0; i < 5; i++)
        {
            IElement? loadMoreBtn = await page.QuerySelectorAsync("#load-more-btn");
            if (loadMoreBtn == null)
            {
                break;
            }

            string? btnText = await loadMoreBtn.GetTextContentAsync();
            if (btnText?.Contains("No More") == true)
            {
                break;
            }

            await loadMoreBtn.ClickAsync();
            await Task.Delay(500);

            string total = await page.EvaluateAsync<string>("document.getElementById('total-count').textContent");
            string unique = await page.EvaluateAsync<string>("document.getElementById('unique-count').textContent");
            string duplicates = await page.EvaluateAsync<string>("document.getElementById('duplicate-count').textContent");

            _output.WriteLine($"After click {i + 1} - Total: {total}, Unique: {unique}, Duplicates: {duplicates}");
        }

        // Assert - Duplicates should be detected
        string? finalTotal = await page.EvaluateAsync<string>("document.getElementById('total-count').textContent");
        string? finalUnique = await page.EvaluateAsync<string>("document.getElementById('unique-count').textContent");
        string? finalDuplicates = await page.EvaluateAsync<string>("document.getElementById('duplicate-count').textContent");

        Assert.True(int.Parse(finalDuplicates ?? "0", CultureInfo.InvariantCulture) > 0, $"Expected duplicates to be detected, got {finalDuplicates}");
        Assert.True(int.Parse(finalUnique ?? "0", CultureInfo.InvariantCulture) < int.Parse(finalTotal ?? "0", CultureInfo.InvariantCulture), $"Unique count should be less than total. Unique: {finalUnique}, Total: {finalTotal}");

        _output.WriteLine($"Final - Total: {finalTotal}, Unique: {finalUnique}, Duplicates: {finalDuplicates}");
    }

    [Fact]
    public async Task DuplicateChunkReplay_Dedupe_HighlightsDuplicateItems()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/duplicate-chunk";

        // Act
        await page.NavigateAsync(url);

        // Click "Load More" to trigger duplicate chunks
        for (int i = 0; i < 4; i++)
        {
            IElement? loadMoreBtn = await page.QuerySelectorAsync("#load-more-btn");
            if (loadMoreBtn == null)
            {
                break;
            }

            await loadMoreBtn.ClickAsync();
            await Task.Delay(500);
        }

        // Assert - Duplicate items should be highlighted
        IReadOnlyList<IElement> duplicateJobs = await page.QuerySelectorAllAsync(".job.duplicate");
        Assert.True(duplicateJobs.Count > 0, $"Expected duplicate jobs to be highlighted, got {duplicateJobs.Count}");

        _output.WriteLine($"Duplicate jobs highlighted: {duplicateJobs.Count}");
    }

    [Fact]
    public async Task DuplicateChunkReplay_Completeness_AllItemsExtracted()
    {
        // Arrange
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();
        string url = $"{_scenarioServer!.BaseUrl}/scenario/scroll/duplicate-chunk";

        // Act
        await page.NavigateAsync(url);

        // Click until end
        for (int i = 0; i < 20; i++)
        {
            IElement? loadMoreBtn = await page.QuerySelectorAsync("#load-more-btn");
            if (loadMoreBtn == null)
            {
                break;
            }

            string? btnText = await loadMoreBtn.GetTextContentAsync();
            if (btnText?.Contains("No More") == true)
            {
                break;
            }

            await loadMoreBtn.ClickAsync();
            await Task.Delay(500);
        }

        // Assert - Completeness: All unique items should be extracted
        string? finalUnique = await page.EvaluateAsync<string>("document.getElementById('unique-count').textContent");
        Assert.True(int.Parse(finalUnique ?? "0", CultureInfo.InvariantCulture) >= 50, $"Expected at least 50 unique jobs, got {finalUnique}");

        _output.WriteLine($"Total unique jobs extracted: {finalUnique}");
    }

    [Fact]
    public async Task AllScrollVariants_AreAccessibleViaServer()
    {
        // Arrange
        string[] expectedScenarios = new[]
        {
            "/scenario/scroll/auto-threshold",
            "/scenario/scroll/button-driven",
            "/scenario/scroll/virtualized",
            "/scenario/scroll/duplicate-chunk"
        };

        // Act
        await using IBrowserSession session = await _browserFixture.CreateSessionAsync();
        IPage page = await session.NewPageAsync();

        foreach (string? scenario in expectedScenarios)
        {
            string url = $"{_scenarioServer!.BaseUrl}{scenario}";
            await page.NavigateAsync(url);
            string? title = await page.GetTitleAsync();
            Assert.NotNull(title);
            _output.WriteLine($"✓ {scenario} is accessible");
        }

        // Assert
        _output.WriteLine($"All {expectedScenarios.Length} scroll variants are accessible");
    }
}
