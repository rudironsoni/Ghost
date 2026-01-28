using System;
using System.Threading.Tasks;
using Xunit;

using Ghost.Core;

namespace Ghost.Core.Tests.Integration;

public class GhostKernelIntegrationTests
{
    // This is an integration test that launches the real browser via Playwright.
    // It verifies that when stealth is enabled the init script alters navigator properties
    // as expected (navigator.webdriver undefined, languages present, plugins array-like).
    [Fact]
    public async Task StealthScript_IsInjected_VerifyNavigatorProperties()
    {
        var options = new KernelOptions
        {
            EnableStealth = true,
            Headless = true
        };

        var kernel = await GhostKernel.CreateAsync(options);

        try
        {
            var session = await kernel.NewSessionAsync();
            await using (session)
            {
                var page = await session.NewPageAsync();
                await using (page)
                {
                    // 1) navigator.webdriver should be undefined (maps to null on .NET side)
                    var webdriver = await page.EvaluateAsync<object>("() => navigator.webdriver");
                    Assert.Null(webdriver);

                    // 2) navigator.languages should be present and have values
                    var languages = await page.EvaluateAsync<string[]>("() => navigator.languages || []");
                    Assert.NotNull(languages);
                    Assert.True(languages.Length > 0, "navigator.languages should contain at least one language");

                    // 3) navigator.plugins is array-like: ensure it has a numeric length and an item function
                    var pluginsInfo = await page.EvaluateAsync<PluginsInfo>(
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
