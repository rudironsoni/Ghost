using Ghost.Contracts.Social;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn Social Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInSocialClientE2ETests
{
    private readonly LinkedInE2EFixture _fixture;

    public LinkedInSocialClientE2ETests(LinkedInE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetProfileAsync_WithValidProfileId_ReturnsProfile()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<LinkedInSocialClient>();
        var profileId = "john-doe";

        // Act
        var result = await client.GetProfileAsync(profileId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LinkedIn", client.PlatformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchProfilesAsync_WithValidCriteria_ReturnsProfiles()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<LinkedInSocialClient>();
        var criteria = new ProfileSearchCriteria
        {
            Query = "Software Engineer",
            MaxResults = 10
        };

        // Act
        var results = await client.SearchProfilesAsync(criteria);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<LinkedInSocialClient>();

        // Act
        var platformName = client.PlatformName;

        // Assert
        Assert.Equal("LinkedIn", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetConnectionsAsync_ReturnsConnections()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<LinkedInSocialClient>();

        // Act
        var results = await client.GetConnectionsAsync();

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SendConnectionRequestAsync_DoesNotThrow()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<LinkedInSocialClient>();
        var profileId = "test-profile";
        var message = "Hello, I'd like to connect!";

        // Act & Assert - Should complete without throwing
        await client.SendConnectionRequestAsync(profileId, message);
    }
}
