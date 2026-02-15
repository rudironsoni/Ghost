using Xunit;

namespace Ghost.Platform.Google.Tests;

/// <summary>
/// Defines a test collection to prevent parallel execution.
/// Required because tests may interact with shared resources or static state.
/// </summary>
[CollectionDefinition("GooglePlatformTests", DisableParallelization = true)]
public class GooglePlatformTestsCollectionDefinition
{
}
