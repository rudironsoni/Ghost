using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Plugin.Glassdoor.End2EndTests.Fixtures;

/// <summary>
/// Collection definition for Glassdoor E2E tests using real browser infrastructure.
/// </summary>
[CollectionDefinition("GlassdoorEnd2End")]
public class BrowserDefinition : ICollectionFixture<RealBrowserFixture>
{
}
