using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;

/// <summary>
/// Collection definition for LinkedIn E2E tests using real browser infrastructure.
/// </summary>
[CollectionDefinition("LinkedInEnd2End")]
public class BrowserDefinition : ICollectionFixture<RealBrowserFixture>
{
}
