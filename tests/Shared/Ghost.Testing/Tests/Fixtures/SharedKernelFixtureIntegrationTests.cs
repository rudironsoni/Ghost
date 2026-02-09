using FluentAssertions;
using Ghost.Core;
using Ghost.Testing.Fixtures;
using Xunit;

namespace Ghost.Testing.Tests.Fixtures;

/// <summary>
/// Integration tests for SharedGhostKernelFixture demonstrating shared kernel usage.
/// </summary>
[Collection("SharedKernel")]
[Trait("Category", "Integration")]
public class SharedKernelFixtureIntegrationTests
{
    private readonly SharedGhostKernelFixture _fixture;

    public SharedKernelFixtureIntegrationTests(SharedGhostKernelFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SharedKernel_IsInitialized()
    {
        // Act
        var kernel = _fixture.Kernel;

        // Assert
        kernel.Should().NotBeNull();
    }

    [Fact]
    public async Task SharedKernel_CanCreateSession()
    {
        // Act
        var session = await _fixture.CreateSessionAsync();

        // Assert
        session.Should().NotBeNull();
        await session.DisposeAsync();
    }

    [Fact]
    public async Task SharedKernel_MultipleSessionsAreIndependent()
    {
        // Arrange & Act
        var session1 = await _fixture.CreateSessionAsync();
        var session2 = await _fixture.CreateSessionAsync();

        // Assert
        session1.Should().NotBeNull();
        session2.Should().NotBeNull();
        session1.Should().NotBeSameAs(session2, "Sessions should be independent");

        await session1.DisposeAsync();
        await session2.DisposeAsync();
    }
}
