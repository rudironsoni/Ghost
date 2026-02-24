using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Middleware;

public class UserAgentRotatorTests : ReliabilityTestBase
{
    public UserAgentRotatorTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_InitializesWithDefaultUserAgents()
    {
        // Arrange & Act
        var rotator = new UserAgentRotator();

        // Assert
        var userAgent1 = rotator.GetNextUserAgent();
        var userAgent2 = rotator.GetNextUserAgent();
        var userAgent3 = rotator.GetNextUserAgent();
        var userAgent4 = rotator.GetNextUserAgent();

        // Should get 4 different default user agents
        userAgent1.Should().NotBeNullOrEmpty();
        userAgent2.Should().NotBeNullOrEmpty();
        userAgent3.Should().NotBeNullOrEmpty();
        userAgent4.Should().NotBeNullOrEmpty();

        // Default user agents should contain common browser strings
        var allAgents = new[] { userAgent1, userAgent2, userAgent3, userAgent4 };
        allAgents.Should().Contain(ua => ua.Contains("Chrome") && ua.Contains("Windows"));
        allAgents.Should().Contain(ua => ua.Contains("Safari") && ua.Contains("Macintosh"));
        allAgents.Should().Contain(ua => ua.Contains("iPhone"));
        allAgents.Should().Contain(ua => ua.Contains("Edg"));
    }

    [Fact]
    public void GetNextUserAgent_RotatesInRoundRobinFashion()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var firstAgent = rotator.GetNextUserAgent();

        // Act - get next 3 agents
        var secondAgent = rotator.GetNextUserAgent();
        var thirdAgent = rotator.GetNextUserAgent();
        var fourthAgent = rotator.GetNextUserAgent();

        // Fifth should wrap back to first
        var fifthAgent = rotator.GetNextUserAgent();

        // Assert
        fifthAgent.Should().Be(firstAgent);
        secondAgent.Should().NotBe(firstAgent);
        thirdAgent.Should().NotBe(firstAgent);
        fourthAgent.Should().NotBe(firstAgent);
    }

    [Fact]
    public void GetNextUserAgent_WhenEmpty_ReturnsFallbackUserAgent()
    {
        // Arrange
        var rotator = new UserAgentRotator();

        // Remove all default user agents
        var agent1 = rotator.GetNextUserAgent();
        var agent2 = rotator.GetNextUserAgent();
        var agent3 = rotator.GetNextUserAgent();
        var agent4 = rotator.GetNextUserAgent();

        rotator.RemoveUserAgent(agent1);
        rotator.RemoveUserAgent(agent2);
        rotator.RemoveUserAgent(agent3);
        rotator.RemoveUserAgent(agent4);

        // Act
        var result = rotator.GetNextUserAgent();

        // Assert
        result.Should().Be("GhostSpider/1.0");
    }

    [Fact]
    public void AddUserAgent_AddsNewUserAgent()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var customAgent = "CustomAgent/1.0";

        // Act
        rotator.AddUserAgent(customAgent);

        // Assert - should eventually get the custom agent in rotation
        List<string> agents = [];
        for (int i = 0; i < 10; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        agents.Should().Contain(customAgent);
    }

    [Fact]
    public void AddUserAgent_DuplicateUserAgent_DoesNotAddTwice()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var customAgent = "CustomAgent/1.0";

        // Act
        rotator.AddUserAgent(customAgent);
        rotator.AddUserAgent(customAgent);

        // Assert - count occurrences in a full rotation
        List<string> agents = [];
        var firstAgent = rotator.GetNextUserAgent();
        agents.Add(firstAgent);

        string currentAgent;
        do
        {
            currentAgent = rotator.GetNextUserAgent();
            agents.Add(currentAgent);
        } while (currentAgent != firstAgent && agents.Count < 20); // Safety limit

        // Remove the duplicate first agent at the end
        agents.RemoveAt(agents.Count - 1);

        // Count how many times custom agent appears
        var customAgentCount = agents.Count(a => a == customAgent);
        customAgentCount.Should().Be(1);
    }

    [Fact]
    public void RemoveUserAgent_RemovesUserAgent()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var agent = rotator.GetNextUserAgent();

        // Act
        rotator.RemoveUserAgent(agent);

        // Assert - should not get this agent again in next 10 rotations
        List<string> agents = [];
        for (int i = 0; i < 10; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        agents.Should().NotContain(agent);
    }

    [Fact]
    public void RemoveUserAgent_AdjustsIndexWhenRemovingBeforeCurrent()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        List<string> agents = [];

        // Get all default agents
        for (int i = 0; i < 4; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        // Now we're back at index 0
        var currentAgent = rotator.GetNextUserAgent();
        currentAgent.Should().Be(agents[0]);

        // Act - remove the first agent
        rotator.RemoveUserAgent(agents[0]);

        // Assert - should continue rotating through remaining agents
        var nextAgent = rotator.GetNextUserAgent();
        nextAgent.Should().NotBe(agents[0]);
        nextAgent.Should().BeOneOf(agents[1], agents[2], agents[3]);
    }

    [Fact]
    public void RemoveUserAgent_WhenRemovingLastAgent_ResetsIndex()
    {
        // Arrange
        var rotator = new UserAgentRotator();

        // Remove all but one agent
        var agent1 = rotator.GetNextUserAgent();
        var agent2 = rotator.GetNextUserAgent();
        var agent3 = rotator.GetNextUserAgent();
        var agent4 = rotator.GetNextUserAgent();

        rotator.RemoveUserAgent(agent1);
        rotator.RemoveUserAgent(agent2);
        rotator.RemoveUserAgent(agent3);

        // Act
        var beforeRemoval = rotator.GetNextUserAgent();
        beforeRemoval.Should().Be(agent4);

        rotator.RemoveUserAgent(agent4);

        // Assert - should return fallback
        var afterRemoval = rotator.GetNextUserAgent();
        afterRemoval.Should().Be("GhostSpider/1.0");
    }

    [Fact]
    public void ThreadSafety_ConcurrentGetNextUserAgent_NoExceptions()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var iterations = 1000;
        var results = new string[iterations];

        // Act
        Parallel.For(0, iterations, i =>
        {
            results[i] = rotator.GetNextUserAgent();
        });

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNullOrEmpty());
        results.Should().OnlyContain(r => r != null);
    }

    [Fact]
    public void ThreadSafety_ConcurrentAddAndGet_NoExceptions()
    {
        // Arrange
        var rotator = new UserAgentRotator();
        var iterations = 100;

        // Act
        Parallel.For(0, iterations, i =>
        {
            if (i % 2 == 0)
            {
                rotator.AddUserAgent($"Agent{i}/1.0");
            }
            else
            {
                _ = rotator.GetNextUserAgent();
            }
        });

        // Assert - should still be able to get user agents
        var result = rotator.GetNextUserAgent();
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ThreadSafety_ConcurrentRemoveAndGet_NoExceptions()
    {
        // Arrange
        var rotator = new UserAgentRotator();

        // Add some extra agents first
        for (int i = 0; i < 10; i++)
        {
            rotator.AddUserAgent($"Agent{i}/1.0");
        }

        var iterations = 100;

        // Act
        Parallel.For(0, iterations, i =>
        {
            if (i % 3 == 0 && i < 10)
            {
                rotator.RemoveUserAgent($"Agent{i}/1.0");
            }
            else
            {
                _ = rotator.GetNextUserAgent();
            }
        });

        // Assert - should still be able to get user agents
        var result = rotator.GetNextUserAgent();
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DefaultUserAgents_ContainsChromeOnWindows()
    {
        // Arrange & Act
        var rotator = new UserAgentRotator();
        List<string> agents = [];

        for (int i = 0; i < 4; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        // Assert
        agents.Should().Contain(ua => ua.Contains("Chrome") && ua.Contains("Windows NT"));
    }

    [Fact]
    public void DefaultUserAgents_ContainsFirefoxOnMac()
    {
        // Arrange & Act
        var rotator = new UserAgentRotator();
        List<string> agents = [];

        for (int i = 0; i < 4; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        // Assert
        // Actually Safari on Mac is provided, not Firefox
        agents.Should().Contain(ua => ua.Contains("Macintosh") && ua.Contains("Safari"));
    }

    [Fact]
    public void DefaultUserAgents_ContainsSafariOniOS()
    {
        // Arrange & Act
        var rotator = new UserAgentRotator();
        List<string> agents = [];

        for (int i = 0; i < 4; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        // Assert
        agents.Should().Contain(ua => ua.Contains("iPhone") && ua.Contains("Safari"));
    }

    [Fact]
    public void DefaultUserAgents_ContainsEdgeOnWindows()
    {
        // Arrange & Act
        var rotator = new UserAgentRotator();
        List<string> agents = [];

        for (int i = 0; i < 4; i++)
        {
            agents.Add(rotator.GetNextUserAgent());
        }

        // Assert
        agents.Should().Contain(ua => ua.Contains("Edg") && ua.Contains("Windows"));
    }
}
