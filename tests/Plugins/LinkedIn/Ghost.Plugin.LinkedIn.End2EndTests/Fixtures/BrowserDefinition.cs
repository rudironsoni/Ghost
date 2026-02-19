using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;

/// <summary>
/// Collection definition for LinkedIn E2E tests using real browser infrastructure.
/// Uses DisableParallelization to prevent resource contention when running multiple E2E tests.
/// </summary>
[CollectionDefinition("LinkedInEnd2End", DisableParallelization = true)]
public class BrowserDefinition : ICollectionFixture<RealBrowserFixture>
{
}
