using Xunit;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// xUnit collection definition for integration tests that share a GhostKernel instance.
/// Tests in the same collection run sequentially and share the SharedGhostKernelFixture.
/// This ensures only ONE browser instance is created across all integration tests.
/// 
/// Usage:
/// [Collection("SharedKernel")]
/// public class MyIntegrationTests
/// {
///     private readonly SharedGhostKernelFixture _fixture;
///     
///     public MyIntegrationTests(SharedGhostKernelFixture fixture)
///     {
///         _fixture = fixture;
///     }
/// }
/// </summary>
[CollectionDefinition("SharedKernel")]
public class SharedKernelCollectionDefinition : ICollectionFixture<SharedGhostKernelFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
