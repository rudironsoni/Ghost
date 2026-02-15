using System;
using System.Threading.Tasks;
using Ghost.Core;
using Xunit;

namespace Ghost.Core.Tests.Integration;

public class GhostKernelIntegrationTests
{
    // This is an integration test that launches the real browser via Playwright.
    // It verifies that when stealth is enabled the init script alters navigator properties
    // as expected (navigator.webdriver undefined, languages present, plugins array-like).
    [Fact]
    public async Task StealthScriptIsInjectedVerifyNavigatorProperties()
    {
        var options = new KernelOptions
        {
            EnableStealth = true,
            Headless = true
        };

        GhostKernel kernel = await GhostKernel.CreateAsync(options);

        try
        {
            IBrowserSession session = await kernel.NewSessionAsync();
            await using (session)
            {
                IPage page = await session.NewPageAsync();
                await using (page)
                {
                    // 1) navigator.webdriver should be undefined or false (both indicate stealth is working)
                    object? webdriver = await page.EvaluateAsync<object>("() => navigator.webdriver");
                    // Accept both null (undefined in JS) and false as valid stealth indicators
                    Assert.True(webdriver is null or false, $"navigator.webdriver should be null or false, but was {webdriver}");

                    // 2) navigator.languages should be present and have values
                    string[] languages = await page.EvaluateAsync<string[]>("() => navigator.languages || []");
                    Assert.NotNull(languages);
                    Assert.True(languages.Length > 0, "navigator.languages should contain at least one language");

                    // 3) navigator.plugins is array-like: ensure it has a numeric length and an item function
                    PluginsInfo pluginsInfo = await page.EvaluateAsync<PluginsInfo>(
                        "() => ({ length: navigator.plugins ? navigator.plugins.length : 0, hasItem: !!(navigator.plugins && typeof navigator.plugins.item === 'function'), isArray: Array.isArray(navigator.plugins) })");

                    Assert.NotNull(pluginsInfo);
                    Assert.True(pluginsInfo.Length >= 0, "navigator.plugins.length should be a number >= 0");
                    Assert.True(pluginsInfo.HasItem, "navigator.plugins should expose an item function (array-like)");
                }
            }
        }
        finally
        {
            await kernel.DisposeAsync();
        }
    }

    private sealed class PluginsInfo
    {
        public int Length { get; set; }
        public bool HasItem { get; set; }
        public bool IsArray { get; set; }
    }
}
