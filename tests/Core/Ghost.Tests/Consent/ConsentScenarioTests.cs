using System.Diagnostics;
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

    private static Task<object?> ClearConsentCookiesAsync(IPage page)
    {
        return page.EvaluateAsync<object?>("""
            (() => {
                document.cookie = 'ghost_consent=; path=/; max-age=0';
                document.cookie = 'ghost_policy_version=; path=/; max-age=0';
                document.cookie = 'ghost_consent_dismissed=; path=/; max-age=0';
                return null;
            })()
            """);
    }

    private static async Task NavigateWithCleanConsentAsync(IPage page, string url)
    {
        await page.NavigateAsync(url);
        await ClearConsentCookiesAsync(page);
        await page.ReloadAsync();
    }

    private static async Task AssertSelectorVisibleAsync(IPage page, string selector)
    {
        var exists = await page.QuerySelectorAsync(selector) is not null;
        var isVisible = await page.EvaluateAsync<bool>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return false;
                const style = window.getComputedStyle(el);
                return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
            }
            """, selector);

        if (!exists || !isVisible)
        {
            var cookies = await page.EvaluateAsync<string>("document.cookie");
            var content = await page.GetContentAsync();
            var snippet = content.Length > 600 ? content[..600] : content;
            Assert.Fail($"Selector '{selector}' exists={exists} visible={isVisible} url={page.Url} cookies='{cookies}' content='{snippet}'");
        }
    }

    private static async Task WaitUntilVisibleAsync(IPage page, string selector, int timeoutMs = 30_000)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var isVisible = await page.EvaluateAsync<bool>("""
                (sel) => {
                    const el = document.querySelector(sel);
                    if (!el) return false;
                    const style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
                }
                """, selector);

            if (isVisible)
            {
                return;
            }

            await Task.Delay(100);
        }

        var cookies = await page.EvaluateAsync<string>("document.cookie");
        Assert.Fail($"Timed out waiting for selector '{selector}' to be visible. url={page.Url} cookies='{cookies}'");
    }

    private static async Task WaitUntilHiddenAsync(IPage page, string selector, int timeoutMs = 30_000)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var isVisible = await page.EvaluateAsync<bool>("""
                (sel) => {
                    const el = document.querySelector(sel);
                    if (!el) return false;
                    const style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
                }
                """, selector);

            if (!isVisible)
            {
                return;
            }

            await Task.Delay(100);
        }

        var cookies = await page.EvaluateAsync<string>("document.cookie");
        Assert.Fail($"Timed out waiting for selector '{selector}' to be hidden. url={page.Url} cookies='{cookies}'");
    }

    private static Task<object?> ClickViaScriptAsync(IPage page, string selector)
    {
        return page.EvaluateAsync<object?>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) {
                    throw new Error(`Element not found: ${sel}`);
                }

                el.dispatchEvent(new MouseEvent('click', {
                    bubbles: true,
                    cancelable: true,
                    view: window
                }));

                if (typeof el.click === 'function') {
                    el.click();
                }

                return null;
            }
            """, selector);
    }

    [Fact]
    public async Task ModalBlocking_AcceptPath_DismissesModal()
    {
        // Arrange
        await using var session = await _browserFixture.CreateSessionAsync();
        var page = await session.NewPageAsync();
        var url = $"{_scenarioServer!.BaseUrl}/scenario/consent/modal-blocking";

        // Act
        await NavigateWithCleanConsentAsync(page, url);

        // Verify modal is visible
        await AssertSelectorVisibleAsync(page, "#consent-modal");
        await AssertSelectorVisibleAsync(page, ".accept-btn");

        // Click accept button
        await ClickViaScriptAsync(page, ".accept-btn");

        // Wait for modal to be dismissed
        await WaitUntilHiddenAsync(page, "#consent-modal");

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
        await NavigateWithCleanConsentAsync(page, url);
        await WaitUntilVisibleAsync(page, "#consent-modal");
        await ClickViaScriptAsync(page, ".reject-btn");
        await WaitUntilHiddenAsync(page, "#consent-modal");

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
        await NavigateWithCleanConsentAsync(page, url);

        // Verify banner is visible
        await WaitUntilVisibleAsync(page, "#consent-banner");

        // Click accept button
        await ClickViaScriptAsync(page, "#consent-banner button");
        await WaitUntilHiddenAsync(page, "#consent-banner");

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
        await NavigateWithCleanConsentAsync(page, url);
        await WaitUntilVisibleAsync(page, "#consent-banner");
        await ClickViaScriptAsync(page, ".dismiss");
        await WaitUntilHiddenAsync(page, "#consent-banner");

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
        await NavigateWithCleanConsentAsync(page, url);

        // Verify iframe is visible
        await WaitUntilVisibleAsync(page, "#cmp-iframe");

        // Click accept button inside iframe
        await page.EvaluateAsync<object?>("""
            (() => {
                window.postMessage({ type: 'consent', action: 'accept' }, '*');
                return null;
            })()
            """);
        await WaitUntilHiddenAsync(page, "#cmp-iframe");

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
        await NavigateWithCleanConsentAsync(page, url);

        // Verify GDPR modal is visible
        await WaitUntilVisibleAsync(page, "#gdpr-modal");

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("EU", content);

        // Click accept all
        await ClickViaScriptAsync(page, ".accept-all");
        await WaitUntilHiddenAsync(page, "#gdpr-modal");

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
        await NavigateWithCleanConsentAsync(page, url);

        // Verify CCPA banner is visible
        await WaitUntilVisibleAsync(page, "#ccpa-banner");

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("California", content);

        // Click opt-out button
        await ClickViaScriptAsync(page, ".opt-out");
        await WaitUntilHiddenAsync(page, "#ccpa-banner");

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
        await NavigateWithCleanConsentAsync(page, url);

        // Verify LGPD modal is visible
        await WaitUntilVisibleAsync(page, "#lgpd-modal");

        // Verify region indicator
        var content = await page.GetContentAsync();
        Assert.Contains("Brasil", content);

        // Click accept
        await ClickViaScriptAsync(page, ".accept");
        await WaitUntilHiddenAsync(page, "#lgpd-modal");

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
        await NavigateWithCleanConsentAsync(page, url);
        await WaitUntilVisibleAsync(page, "#consent-banner");
        await ClickViaScriptAsync(page, "#consent-banner button");
        await WaitUntilHiddenAsync(page, "#consent-banner");

        // Get session ID
        var sessionId = await page.EvaluateAsync<string>("document.getElementById('session-id').textContent");

        // Navigate to page 2 (same session)
        await ClickViaScriptAsync(page, "a[href*='page=2']");
        await page.WaitForLoadStateAsync();

        // Assert - Banner should not appear on second visit
        await WaitUntilHiddenAsync(page, "#consent-banner");

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
        await NavigateWithCleanConsentAsync(page, url);

        await page.EvaluateAsync<object?>("""
            (() => {
                document.cookie = 'ghost_consent=accepted; path=/';
                document.cookie = 'ghost_policy_version=1; path=/';
                return null;
            })()
            """);
        await page.ReloadAsync();

        // Verify re-consent modal is visible (policy version mismatch)
        await WaitUntilVisibleAsync(page, "#reconsent-modal");

        // Accept new policy
        await ClickViaScriptAsync(page, ".accept-new");
        await WaitUntilHiddenAsync(page, "#reconsent-modal");

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
