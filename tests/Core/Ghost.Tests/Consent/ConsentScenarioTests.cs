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
        await page.NavigateAsync(url).ConfigureAwait(false);
        await ClearConsentCookiesAsync(page).ConfigureAwait(false);
        await page.ReloadAsync().ConfigureAwait(false);
    }

    private static async Task AssertSelectorVisibleAsync(IPage page, string selector)
    {
        bool exists = await page.QuerySelectorAsync(selector).ConfigureAwait(false) is not null;
        bool isVisible = await page.EvaluateAsync<bool>("""
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return false;
                const style = window.getComputedStyle(el);
                return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
            }
            """, selector).ConfigureAwait(false);

        if (!exists || !isVisible)
        {
            string cookies = await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false);
            string content = await page.GetContentAsync().ConfigureAwait(false);
            string snippet = content.Length > 600 ? content[..600] : content;
            Assert.Fail($"Selector '{selector}' exists={exists} visible={isVisible} url={page.Url} cookies='{cookies}' content='{snippet}'");
        }
    }

    private static async Task WaitUntilVisibleAsync(IPage page, string selector, int timeoutMs = 30_000)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            bool isVisible = await page.EvaluateAsync<bool>("""
                (sel) => {
                    const el = document.querySelector(sel);
                    if (!el) return false;
                    const style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
                }
                """, selector).ConfigureAwait(false);

            if (isVisible)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        string cookies = await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false);
        Assert.Fail($"Timed out waiting for selector '{selector}' to be visible. url={page.Url} cookies='{cookies}'");
    }

    private static async Task WaitUntilHiddenAsync(IPage page, string selector, int timeoutMs = 30_000)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            bool isVisible = await page.EvaluateAsync<bool>("""
                (sel) => {
                    const el = document.querySelector(sel);
                    if (!el) return false;
                    const style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
                }
                """, selector).ConfigureAwait(false);

            if (!isVisible)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        string cookies = await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false);
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
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/modal-blocking";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify modal is visible
        await AssertSelectorVisibleAsync(page, "#consent-modal").ConfigureAwait(false);
        await AssertSelectorVisibleAsync(page, ".accept-btn").ConfigureAwait(false);

        // Click accept button
        await ClickViaScriptAsync(page, ".accept-btn").ConfigureAwait(false);

        // Wait for modal to be dismissed
        await WaitUntilHiddenAsync(page, "#consent-modal").ConfigureAwait(false);

        _output.WriteLine("Modal blocking accept path test passed");
    }

    [Fact]
    public async Task ModalBlocking_RejectPath_DismissesModal()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/modal-blocking";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);
        await WaitUntilVisibleAsync(page, "#consent-modal").ConfigureAwait(false);
        await ClickViaScriptAsync(page, ".reject-btn").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#consent-modal").ConfigureAwait(false);

        _output.WriteLine("Modal blocking reject path test passed");
    }

    [Fact]
    public async Task BannerSoft_AcceptPath_DismissesBanner()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/banner-soft";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify banner is visible
        await WaitUntilVisibleAsync(page, "#consent-banner").ConfigureAwait(false);

        // Click accept button
        await ClickViaScriptAsync(page, "#consent-banner button").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#consent-banner").ConfigureAwait(false);

        _output.WriteLine("Soft banner accept path test passed");
    }

    [Fact]
    public async Task BannerDismiss_DismissWithoutDecision_SetsDismissedState()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/banner-dismiss";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);
        await WaitUntilVisibleAsync(page, "#consent-banner").ConfigureAwait(false);
        await ClickViaScriptAsync(page, ".dismiss").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#consent-banner").ConfigureAwait(false);

        _output.WriteLine("Banner dismiss without decision test passed");
    }

    [Fact]
    public async Task IframeCmp_AcceptViaPostMessage_DismissesIframe()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/iframe-cmp";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify iframe is visible
        await WaitUntilVisibleAsync(page, "#cmp-iframe").ConfigureAwait(false);

        // Click accept button inside iframe
        await page.EvaluateAsync<object?>("""
            (() => {
                window.postMessage({ type: 'consent', action: 'accept' }, '*');
                return null;
            })()
            """).ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#cmp-iframe").ConfigureAwait(false);

        _output.WriteLine("Iframe CMP accept via postMessage test passed");
    }

    [Fact]
    public async Task RegionGdpr_ExplicitConsentRequired_ShowsBlockingModal()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-gdpr";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify GDPR modal is visible
        await WaitUntilVisibleAsync(page, "#gdpr-modal").ConfigureAwait(false);

        // Verify region indicator
        string content = await page.GetContentAsync().ConfigureAwait(false);
        Assert.Contains("EU", content);

        // Click accept all
        await ClickViaScriptAsync(page, ".accept-all").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#gdpr-modal").ConfigureAwait(false);

        _output.WriteLine("GDPR region-specific consent test passed");
    }

    [Fact]
    public async Task RegionCcpa_OptOutModel_AllowsDefaultWithOptOutOption()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-ccpa";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify CCPA banner is visible
        await WaitUntilVisibleAsync(page, "#ccpa-banner").ConfigureAwait(false);

        // Verify region indicator
        string content = await page.GetContentAsync().ConfigureAwait(false);
        Assert.Contains("California", content);

        // Click opt-out button
        await ClickViaScriptAsync(page, ".opt-out").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#ccpa-banner").ConfigureAwait(false);

        _output.WriteLine("CCPA opt-out model test passed");
    }

    [Fact]
    public async Task RegionLgpd_SimilarToGdpr_RequiresExplicitConsent()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/region-lgpd";

        // Act
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        // Verify LGPD modal is visible
        await WaitUntilVisibleAsync(page, "#lgpd-modal").ConfigureAwait(false);

        // Verify region indicator
        string content = await page.GetContentAsync().ConfigureAwait(false);
        Assert.Contains("Brasil", content);

        // Click accept
        await ClickViaScriptAsync(page, ".accept").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#lgpd-modal").ConfigureAwait(false);

        _output.WriteLine("LGPD region-specific consent test passed");
    }

    [Fact]
    public async Task StatefulPersistence_ConsentPersistsAcrossSession()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/stateful-persistence";

        // Act - First visit: accept consent
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);
        await WaitUntilVisibleAsync(page, "#consent-banner").ConfigureAwait(false);
        await ClickViaScriptAsync(page, "#consent-banner button").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#consent-banner").ConfigureAwait(false);

        // Get session ID
        string sessionId = await page.EvaluateAsync<string>("document.getElementById('session-id').textContent").ConfigureAwait(false);

        // Navigate to page 2 (same session)
        await ClickViaScriptAsync(page, "a[href*='page=2']").ConfigureAwait(false);
        await page.WaitForLoadStateAsync().ConfigureAwait(false);

        // Assert - Banner should not appear on second visit
        await WaitUntilHiddenAsync(page, "#consent-banner").ConfigureAwait(false);

        _output.WriteLine("Stateful consent persistence test passed");
    }

    [Fact]
    public async Task ReconsentPolicyChange_PolicyUpdateTriggersReconsent()
    {
        // Arrange
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        string url = $"{_scenarioServer!.BaseUrl}/scenario/consent/reconsent-policy-change";

        // Act - Navigate to page (policy version mismatch triggers re-consent)
        await NavigateWithCleanConsentAsync(page, url).ConfigureAwait(false);

        await page.EvaluateAsync<object?>("""
            (() => {
                document.cookie = 'ghost_consent=accepted; path=/';
                document.cookie = 'ghost_policy_version=1; path=/';
                return null;
            })()
            """).ConfigureAwait(false);
        await page.ReloadAsync().ConfigureAwait(false);

        // Verify re-consent modal is visible (policy version mismatch)
        await WaitUntilVisibleAsync(page, "#reconsent-modal").ConfigureAwait(false);

        // Accept new policy
        await ClickViaScriptAsync(page, ".accept-new").ConfigureAwait(false);
        await WaitUntilHiddenAsync(page, "#reconsent-modal").ConfigureAwait(false);

        _output.WriteLine("Re-consent on policy change test passed");
    }

    [Fact]
    public async Task AllConsentVariants_AreAccessibleViaServer()
    {
        // Arrange
        string[] expectedScenarios = new[]
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
        await using IBrowserSession session = (await _browserFixture.CreateSessionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        foreach (string? scenario in expectedScenarios)
        {
            string url = $"{_scenarioServer!.BaseUrl}{scenario}";
            await page.NavigateAsync(url).ConfigureAwait(false);
            string? title = await page.GetTitleAsync().ConfigureAwait(false);
            Assert.NotNull(title);
            _output.WriteLine($"✓ {scenario} is accessible");
        }

        // Assert
        _output.WriteLine($"All {expectedScenarios.Length} consent variants are accessible");
    }
}
