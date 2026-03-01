using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Dedupe;

/// <summary>
/// Adversarial deduplication test suite.
/// Tests URL normalization and fingerprinting against various adversarial patterns:
/// - Query parameter reordering
/// - Tracking parameter stripping
/// - Redirect chain resolution
/// - Multiple alias handling
/// - Temporal changes
/// - Mixed case parameters
/// - Array parameter ordering
/// - Session tracking parameters
/// - A/B test variants
/// </summary>
[Collection("Browser")]
public class DedupeAdversarialTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RealBrowserFixture _browserFixture;
    private ScenarioServer? _scenarioServer;

    public DedupeAdversarialTests(ITestOutputHelper output, RealBrowserFixture browserFixture)
    {
        _output = output;
        _browserFixture = browserFixture;
    }

    public async Task InitializeAsync()
    {
        _scenarioServer = await ScenarioServer.CreateAsync();
        _output.WriteLine($"Scenario server started at {_scenarioServer.BaseUrl}");
    }

    public async Task DisposeAsync()
    {
        if (_scenarioServer != null)
        {
            await _scenarioServer.StopAsync();
            _scenarioServer.Dispose();
        }
    }

    /// <summary>
    /// Tests that query parameter reordering produces the same fingerprint.
    /// URLs with the same parameters in different orders should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task QueryReorder_DifferentOrders_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/query-reorder?z=3&y=2&x=1",
            $"{baseUrl}/scenario/dedupe/query-reorder?x=1&y=2&z=3",
            $"{baseUrl}/scenario/dedupe/query-reorder?y=2&x=1&z=3"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("Query reorder test passed - all fingerprints match");
    }

    /// <summary>
    /// Tests that tracking parameters are stripped and produce the same fingerprint.
    /// URLs with different tracking parameters should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task TrackingParams_DifferentTracking_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/tracking-params?jobId=123&utm_source=google&utm_campaign=test",
            $"{baseUrl}/scenario/dedupe/tracking-params?jobId=123&fbclid=abc123&ref=facebook",
            $"{baseUrl}/scenario/dedupe/tracking-params?jobId=123"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("Tracking params test passed - all fingerprints match");
    }

    /// <summary>
    /// Tests that redirect chains resolve to the same canonical URL.
    /// Short URLs, tracking redirects, and direct links should all resolve to the same job.
    /// </summary>
    [Fact]
    public async Task RedirectChain_DifferentPaths_ResolveToSameCanonical()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/redirect-chain?step=0",
            $"{baseUrl}/scenario/dedupe/redirect-chain?step=1&tracking=abc123",
            $"{baseUrl}/scenario/dedupe/redirect-chain?step=2&source=email&utm_source=newsletter",
            $"{baseUrl}/scenario/dedupe/redirect-chain?step=final"
        };

        List<string> canonicalIds = [];

        // Act - Visit each URL and extract canonical job ID
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var canonicalId = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Job ID:\\s*([\\w-]+)/)?.[1] || ''");
            canonicalIds.Add(canonicalId);
            _output.WriteLine($"URL: {url}, Canonical ID: {canonicalId}");
        }

        // Assert - All canonical IDs should be identical
        Assert.All(canonicalIds, id => Assert.NotEmpty(id));
        Assert.All(canonicalIds, id => Assert.Equal(canonicalIds[0], id));

        _output.WriteLine("Redirect chain test passed - all resolve to same canonical");
    }

    /// <summary>
    /// Tests that multiple URL aliases resolve to the same job.
    /// Different slugs, mobile URLs, and regional URLs should all resolve to the same logical posting.
    /// </summary>
    [Fact]
    public async Task MultipleAliases_DifferentAliases_ResolveToSameJob()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=slug1",
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=slug2",
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=slug3",
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=mobile",
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=regional",
            $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=default"
        };

        List<string> canonicalIds = [];

        // Act - Visit each URL and extract canonical job ID
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var canonicalId = await page.EvaluateAsync<string>("() => document.querySelector('.canonical')?.textContent?.match(/Job ID:\\s*([\\w-]+)/)?.[1] || ''");
            canonicalIds.Add(canonicalId);
            _output.WriteLine($"URL: {url}, Canonical ID: {canonicalId}");
        }

        // Assert - All canonical IDs should be identical
        Assert.All(canonicalIds, id => Assert.NotEmpty(id));
        Assert.All(canonicalIds, id => Assert.Equal(canonicalIds[0], id));

        _output.WriteLine("Multiple aliases test passed - all resolve to same job");
    }

    /// <summary>
    /// Tests that temporal changes (title updates, reposts) maintain the same logical job ID.
    /// Different versions of the same posting should be recognized as duplicates.
    /// </summary>
    [Fact]
    public async Task TemporalChanges_DifferentVersions_MaintainSameLogicalJob()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/temporal-changes?version=v1",
            $"{baseUrl}/scenario/dedupe/temporal-changes?version=v2",
            $"{baseUrl}/scenario/dedupe/temporal-changes?version=v3"
        };

        List<string> canonicalIds = [];
        List<string> titles = [];

        // Act - Visit each URL and extract canonical job ID and title
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var canonicalId = await page.EvaluateAsync<string>("() => document.querySelector('.canonical')?.textContent?.match(/Canonical Job ID:\\s*([\\w-]+)/)?.[1] || ''");
            var title = await page.EvaluateAsync<string>("() => document.querySelector('.canonical')?.textContent?.match(/Current Title:\\s*([^\\n]+)/)?.[1]?.trim() || ''");
            canonicalIds.Add(canonicalId);
            titles.Add(title);
            _output.WriteLine($"URL: {url}, Canonical ID: {canonicalId}, Title: {title}");
        }

        // Assert - All canonical IDs should be identical (titles may differ)
        Assert.All(canonicalIds, id => Assert.NotEmpty(id));
        Assert.All(canonicalIds, id => Assert.Equal(canonicalIds[0], id));

        // Titles should be different (demonstrating temporal change)
        Assert.NotEqual(titles[0], titles[1]);

        _output.WriteLine("Temporal changes test passed - all versions maintain same logical job");
    }

    /// <summary>
    /// Tests that mixed case parameters produce the same fingerprint.
    /// URLs with parameters in different cases should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task MixedCaseParams_DifferentCases_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/mixed-case-params?JobID=123&Source=LinkedIn",
            $"{baseUrl}/scenario/dedupe/mixed-case-params?jobid=123&source=linkedin",
            $"{baseUrl}/scenario/dedupe/mixed-case-params?JOBID=123&SOURCE=LINKEDIN",
            $"{baseUrl}/scenario/dedupe/mixed-case-params?JoBiD=123&SoUrCe=LiNkEdIn"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("Mixed case params test passed - all fingerprints match");
    }

    /// <summary>
    /// Tests that array parameter ordering produces the same fingerprint.
    /// URLs with the same array parameters in different orders should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task ArrayParams_DifferentOrders_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/array-params?skills=python&skills=java&skills=javascript",
            $"{baseUrl}/scenario/dedupe/array-params?skills=java&skills=python&skills=javascript",
            $"{baseUrl}/scenario/dedupe/array-params?skills=javascript&skills=python&skills=java"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("Array params test passed - all fingerprints match");
    }

    /// <summary>
    /// Tests that session tracking parameters are stripped and produce the same fingerprint.
    /// URLs with different session IDs should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task SessionTracking_DifferentSessions_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456&sessionid=abc123",
            $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456&sid=xyz789",
            $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456&click_id=click123",
            $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456&referral_id=ref456",
            $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("Session tracking test passed - all fingerprints match");
    }

    /// <summary>
    /// Tests that A/B test variant parameters are stripped and produce the same fingerprint.
    /// URLs with different A/B test variants should canonicalize to the same resource.
    /// </summary>
    [Fact]
    public async Task ABTestVariants_DifferentVariants_ProduceSameFingerprint()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var urls = new[]
        {
            $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789&ab_test=A&variant=control",
            $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789&ab_test=B&variant=treatment",
            $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789&experiment=exp1&test_group=group1",
            $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789&bucket=42",
            $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789"
        };

        List<string> fingerprints = [];

        // Act - Visit each URL and extract fingerprint
        foreach (var url in urls)
        {
            await page.GotoAsync(url);
            var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)/)?.[1] || ''");
            fingerprints.Add(fingerprint);
            _output.WriteLine($"URL: {url}, Fingerprint: {fingerprint}");
        }

        // Assert - All fingerprints should be identical
        Assert.All(fingerprints, f => Assert.NotEmpty(f));
        Assert.All(fingerprints, f => Assert.Equal(fingerprints[0], f));

        _output.WriteLine("A/B test variants test passed - all fingerprints match");
    }

    /// <summary>
    /// Comprehensive test that verifies all adversarial patterns produce stable fingerprints.
    /// This is a smoke test that ensures the entire deduplication system works correctly.
    /// </summary>
    [Fact]
    public async Task AllAdversarialPatterns_ProduceStableFingerprints()
    {
        // Arrange
        await using var context = await _browserFixture.NewContextAsync();
        var page = await context.NewPageAsync();
        var baseUrl = _scenarioServer!.BaseUrl;

        var testCases = new[]
        {
            // Query reordering
            new { Name = "Query Reorder", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/query-reorder?z=3&y=2&x=1",
                $"{baseUrl}/scenario/dedupe/query-reorder?x=1&y=2&z=3"
            }},
            // Tracking parameters
            new { Name = "Tracking Params", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/tracking-params?jobId=123&utm_source=google",
                $"{baseUrl}/scenario/dedupe/tracking-params?jobId=123"
            }},
            // Multiple aliases
            new { Name = "Multiple Aliases", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=slug1",
                $"{baseUrl}/scenario/dedupe/multiple-aliases?alias=slug2"
            }},
            // Mixed case
            new { Name = "Mixed Case", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/mixed-case-params?JobID=123",
                $"{baseUrl}/scenario/dedupe/mixed-case-params?jobid=123"
            }},
            // Array params
            new { Name = "Array Params", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/array-params?skills=python&skills=java",
                $"{baseUrl}/scenario/dedupe/array-params?skills=java&skills=python"
            }},
            // Session tracking
            new { Name = "Session Tracking", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456&sessionid=abc",
                $"{baseUrl}/scenario/dedupe/session-tracking?jobId=456"
            }},
            // A/B test variants
            new { Name = "A/B Test Variants", Urls = new[]
            {
                $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789&ab_test=A",
                $"{baseUrl}/scenario/dedupe/ab-test-variants?jobId=789"
            }}
        };

        var allPassed = true;

        // Act & Assert - Test each adversarial pattern
        foreach (var testCase in testCases)
        {
            List<string> fingerprints = [];

            foreach (var url in testCase.Urls)
            {
                await page.GotoAsync(url);
                var fingerprint = await page.EvaluateAsync<string>("() => document.querySelector('.info, .canonical')?.textContent?.match(/Fingerprint:\\s*([A-F0-9]+)|Canonical Fingerprint:\\s*([\\w-]+)/)?.[1] || document.querySelector('.canonical')?.textContent?.match(/Canonical Fingerprint:\\s*([\\w-]+)/)?.[1] || ''");
                fingerprints.Add(fingerprint);
            }

            var allMatch = fingerprints.All(f => f == fingerprints[0]);
            if (!allMatch)
            {
                _output.WriteLine($"FAILED: {testCase.Name} - Fingerprints don't match: {string.Join(", ", fingerprints)}");
                allPassed = false;
            }
            else
            {
                _output.WriteLine($"PASSED: {testCase.Name} - All fingerprints match: {fingerprints[0]}");
            }

            Assert.True(allMatch, $"{testCase.Name} test failed - fingerprints don't match");
        }

        _output.WriteLine("All adversarial patterns test passed - stable fingerprints verified");
    }
}
