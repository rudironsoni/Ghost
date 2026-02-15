using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Ghost.Worker.Tests;

/// <summary>
/// Tests for ScopedProbe test helper.
/// </summary>
public sealed class ScopedProbeTests
{
    [Fact]
    public void ScopedProbe_NotDisposed_ByDefault()
    {
        // Arrange & Act
        using var probe = new ScopedProbe();

        // Assert
        probe.DisposeStarted.Should().BeFalse();
        probe.DisposeCompleted.Should().BeFalse();
    }

    [Fact]
    public void ScopedProbe_EnsureNotDisposed_DoesNotThrow_Initially()
    {
        // Arrange
        using var probe = new ScopedProbe();

        // Act
        Action act = () => probe.EnsureNotDisposed();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ScopedProbe_Dispose_MarksAsDisposed()
    {
        // Arrange
        var probe = new ScopedProbe();

        // Act
        probe.Dispose();

        // Assert
        probe.DisposeStarted.Should().BeTrue();
        probe.DisposeCompleted.Should().BeTrue();
    }

    [Fact]
    public void ScopedProbe_EnsureNotDisposed_Throws_AfterDisposal()
    {
        // Arrange
        var probe = new ScopedProbe();
        probe.Dispose();

        // Act
        Action act = () => probe.EnsureNotDisposed();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ScopedProbe_DisposeStartedTask_Completes_WhenDisposalStarts()
    {
        // Arrange
        var probe = new ScopedProbe();

        // Act
        var disposeTask = Task.Run(() => probe.Dispose());
        await probe.DisposeStartedTask.ConfigureAwait(false);

        // Assert
        probe.DisposeStarted.Should().BeTrue();

        // Cleanup
        await disposeTask.ConfigureAwait(false);
    }

    [Fact]
    public void ScopedProbe_GetState_ReturnsCurrentState()
    {
        // Arrange
        using var probe = new ScopedProbe();

        // Act
        string state = probe.GetState();

        // Assert
        state.Should().Contain("Started: False");
        state.Should().Contain("Completed: False");
    }

    [Fact]
    public async Task ScopedProbe_DisposeAsync_MarksAsDisposed()
    {
        // Arrange
        var probe = new ScopedProbe();

        // Act
        await probe.DisposeAsync().ConfigureAwait(false);

        // Assert
        probe.DisposeStarted.Should().BeTrue();
        probe.DisposeCompleted.Should().BeTrue();
    }
}
