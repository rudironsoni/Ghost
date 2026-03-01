using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests.Fixtures;

/// <summary>
/// Collection definition for Google E2E tests using real browser infrastructure.
/// </summary>
[CollectionDefinition("GoogleEnd2End")]
public class BrowserDefinition : ICollectionFixture<RealBrowserFixture>
{
}
