using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Tests.Consent;

/// <summary>
/// xUnit collection definition for integration tests that share a browser instance.
/// Tests in the same collection run sequentially and share the RealBrowserFixture.
/// Tests in different collections can run in parallel.
/// </summary>
[CollectionDefinition("Browser")]
public class BrowserCollectionFixture : ICollectionFixture<RealBrowserFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
