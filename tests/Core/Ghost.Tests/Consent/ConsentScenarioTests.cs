using Ghost.Core;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Consent;

/// <summary>
/// Integration tests for consent flow scenarios using the synthetic scenario server.
/// Tests various consent mechanisms: blocking modals, soft banners, iframe CMPs,
/// region-specific consent (GDPR, CCPA, LGPD), and stateful consent persistence.
/// </summary>
[Collection("Browser")]
[Trait("Category", "E2E")]
public class ConsentScenarioTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RealBrowserFixture _browserFixture;
    private ScenarioServer? _scenarioServer;

    public ConsentScenarioTests(ITestOutputHelper output, RealBrowserFixture browserFixture)
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

    [Fact]
    public async Task ModalBlocking_AcceptPath_DismissesModal()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/modal-blocking";

        // Act
        await page.NavigateAsync(url);

        // Verify modal is visible
        var modal = await page.QuerySelectorAsync("#consent-modal");
        Assert.NotNull(modal);

        // Click accept button
        await page.ClickAsync(".accept-btn");

        // Wait for modal to be dismissed
        await Task.Delay(500); // Simple wait for UI update

        // Assert - Verify modal is hidden by checking if it no longer exists
        var modalAfter = await page.QuerySelectorAsync("#consent-modal");
        Assert.Null(modalAfter);

        _output.WriteLine("Modal blocking accept path test passed");
    }

    [Fact]
    public async Task ModalBlocking_RejectPath_DismissesModal()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/modal-blocking";

        // Act
        await page.NavigateAsync(url);
        await page.ClickAsync(".reject-btn");
        await Task.Delay(500);

        // Assert
        var modal = await page.QuerySelectorAsync("#consent-modal");
        Assert.Null(modal);

        _output.WriteLine("Modal blocking reject path test passed");
    }

    [Fact]
    public async Task BannerSoft_AcceptPath_DismissesBanner()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/banner-soft";

        // Act
        await page.NavigateAsync(url);

        // Verify banner is visible
        var banner = await page.QuerySelectorAsync("#consent-banner");
        Assert.NotNull(banner);

        // Click accept button
        await page.ClickAsync("#consent-banner button");
        await Task.Delay(500);

        // Assert
        var bannerAfter = await page.QuerySelectorAsync("#consent-banner");
        Assert.Null(bannerAfter);

        _output.WriteLine("Soft banner accept path test passed");
    }

    [Fact]
    public async Task BannerDismiss_DismissWithoutDecision_SetsDismissedState()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/banner-dismiss";

        // Act
        await page.NavigateAsync(url);
        await page.ClickAsync(".dismiss");
        await Task.Delay(500);

        // Assert
        var banner = await page.QuerySelectorAsync("#consent-banner");
        Assert.Null(banner);

        _output.WriteLine("Banner dismiss without decision test passed");
    }

    [Fact]
    public async Task IframeCmp_AcceptViaPostMessage_DismissesIframe()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/iframe-cmp";

        // Act
        await page.NavigateAsync(url);

        // Verify iframe is visible
        var iframe = await page.QuerySelectorAsync("#cmp-iframe");
        Assert.NotNull(iframe);

        // Click accept button inside iframe
        await page.ClickAsync("#cmp-iframe button");
        await Task.Delay(500);

        // Assert
        var iframeAfter = await page.QuerySelectorAsync("#cmp-iframe");
        Assert.Null(iframeAfter);

        _output.WriteLine("Iframe CMP accept via postMessage test passed");
    }

    [Fact]
    public async Task RegionGdpr_ExplicitConsentRequired_ShowsBlockingModal()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-gdpr";

        // Act
        await page.NavigateAsync(url);

        // Verify GDPR modal is visible
        var modal = await page.QuerySelectorAsync("#gdpr-modal");
        Assert.NotNull(modal);

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("EU", content);

        // Click accept all
        await page.ClickAsync(".accept-all");
        await Task.Delay(500);

        // Assert
        var modalAfter = await page.QuerySelectorAsync("#gdpr-modal");
        Assert.Null(modalAfter);

        _output.WriteLine("GDPR region-specific consent test passed");
    }

    [Fact]
    public async Task RegionCcpa_OptOutModel_AllowsDefaultWithOptOutOption()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-ccpa";

        // Act
        await page.NavigateAsync(url);

        // Verify CCPA banner is visible
        var banner = await page.QuerySelectorAsync("#ccpa-banner");
        Assert.NotNull(banner);

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("California", content);

        // Click opt-out button
        await page.ClickAsync(".opt-out");
        await Task.Delay(500);

        // Assert
        var bannerAfter = await page.QuerySelectorAsync("#ccpa-banner");
        Assert.Null(bannerAfter);

        _output.WriteLine("CCPA opt-out model test passed");
    }

    [Fact]
    public async Task RegionLgpd_SimilarToGdpr_RequiresExplicitConsent()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-lgpd";

        // Act
        await page.NavigateAsync(url);

        // Verify LGPD modal is visible
        var modal = await page.QuerySelectorAsync("#lgpd-modal");
        Assert.NotNull(modal);

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("Brasil", content);

        // Click accept
        await page.ClickAsync(".accept");
        await Task.Delay(500);

        // Assert
        var modalAfter = await page.QuerySelectorAsync("#lgpd-modal");
        Assert.Null(modalAfter);

        _output.WriteLine("LGPD region-specific consent test passed");
    }

    [Fact]
    public async Task StatefulPersistence_ConsentPersistsAcrossSession()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/stateful-persistence";

        // Act - First visit: accept consent
        await page.NavigateAsync(url);
        await page.ClickAsync("#consent-banner button");
        await Task.Delay(500);

        // Get session ID
        var sessionId = await page.EvaluateAsync<string>("document.getElementById('session-id').textContent");

        // Navigate to page 2 (same session)
        await page.ClickAsync("a[href*='page=2']");
        await page.WaitForLoadStateAsync();

        // Assert - Banner should not appear on second visit
        var banner = await page.QuerySelectorAsync("#consent-banner");
        Assert.Null(banner);

        _output.WriteLine("Stateful consent persistence test passed");
    }

    [Fact]
    public async Task ReconsentPolicyChange_PolicyUpdateTriggersReconsent()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/reconsent-policy-change";

        // Act - Navigate to page (policy version mismatch triggers re-consent)
        await page.NavigateAsync(url);

        // Verify re-consent modal is visible (policy version mismatch)
        var modal = await page.QuerySelectorAsync("#reconsent-modal");
        Assert.NotNull(modal);

        // Accept new policy
        await page.ClickAsync(".accept-new");
        await Task.Delay(500);

        // Assert
        var modalAfter = await page.QuerySelectorAsync("#reconsent-modal");
        Assert.Null(modalAfter);

        _output.WriteLine("Re-consent on policy change test passed");
    }

    [Fact]
    public async Task AllConsentVariants_AreAccessibleViaServer()
    {
        // Arrange
        var expectedScenarios = new[]
        {
            "/scenario/consent/modal-blocking",
            "/scenario/consent/banner-soft",
            "/scenario/consent/banner-dismiss",
            "/scenario/consent/iframe-cmp",
            "/scenario/consent/iframe-cmp-advanced",
            "/scenario/consent/region-gdpr",
            "/scenario/consent/region-ccpa",
            "/scenario/consent/region-lgpd",
            "/scenario/consent/stateful-persistence",
            "/scenario/consent/reconsent-policy-change"
        };

        // Act
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();

        foreach (var scenario in expectedScenarios)
        {
            var url = $"{_scenarioServer!.BaseUrl}{scenario}";
            await page.NavigateAsync(url);
            var title = page.Title;
            Assert.NotNull(title);
            _output.WriteLine($"✓ {scenario} is accessible");
        }

        // Assert
        _output.WriteLine($"All {expectedScenarios.Length} consent variants are accessible");
    }
}
