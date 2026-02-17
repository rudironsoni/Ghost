using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Plugin.Indeed.End2EndTests.Fixtures;

/// <summary>
/// Collection definition for Indeed E2E tests using real browser infrastructure.
/// </summary>
[CollectionDefinition("Browser")]
public class BrowserDefinition : ICollectionFixture<RealBrowserFixture>
{
}
