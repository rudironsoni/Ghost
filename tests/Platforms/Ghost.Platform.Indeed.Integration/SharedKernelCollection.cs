using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Platform.Indeed.Integration;

/// <summary>
/// xUnit collection definition for integration tests that share a GhostKernel instance.
/// Tests in the same collection run sequentially and share the SharedGhostKernelFixture.
/// </summary>
[CollectionDefinition("SharedKernel")]
public class SharedKernelCollectionDefinition : ICollectionFixture<SharedGhostKernelFixture>
{
}
