using FluentAssertions;
using Ghost.Sdk.Console;
using Xunit;

namespace Ghost.Sdk.Tests.Console;

/// <summary>
/// Unit tests for TelnetSession.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelnetSessionTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var session = new TelnetSession();

        // Assert
        session.SessionId.Should().NotBeEmpty();
        session.ConnectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        session.LastActivity.Should().Be(default);
        session.ClientAddress.Should().BeEmpty();
        session.IsAuthenticated.Should().BeFalse();
        session.CommandHistory.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        // Arrange
        var session = new TelnetSession();
        var sessionId = "test-session-123";
        var connectedAt = DateTimeOffset.UtcNow;
        var lastActivity = DateTimeOffset.UtcNow.AddMinutes(5);
        var clientAddress = "192.168.1.100";

        // Act
        session.SessionId = sessionId;
        session.ConnectedAt = connectedAt;
        session.LastActivity = lastActivity;
        session.ClientAddress = clientAddress;
        session.IsAuthenticated = true;
        session.CommandHistory.Add("help");
        session.CommandHistory.Add("status");

        // Assert
        session.SessionId.Should().Be(sessionId);
        session.ConnectedAt.Should().Be(connectedAt);
        session.LastActivity.Should().Be(lastActivity);
        session.ClientAddress.Should().Be(clientAddress);
        session.IsAuthenticated.Should().BeTrue();
        session.CommandHistory.Should().HaveCount(2);
        session.CommandHistory.Should().Contain("help");
        session.CommandHistory.Should().Contain("status");
    }

    [Fact]
    public void SessionId_GeneratesUniqueIds()
    {
        // Act
        var session1 = new TelnetSession();
        var session2 = new TelnetSession();

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);
    }
}
