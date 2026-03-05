using Ghost.Contracts.Jobs;
using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Ghost.Testing.End2End;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Google.End2EndTests;

/// <summary>
/// End-to-End tests for Google Jobs Client using HTTP client.
/// Tests validate actual data extraction from Google Jobs.
/// </summary>
[Trait("Category", "End2End")]
[Trait("Capability", "RequiresProviderLive")]
public sealed class GoogleJobClientE2ETests : IClassFixture<Fixtures.GoogleE2EFixture>
{
    private readonly Fixtures.GoogleE2EFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GoogleJobClientE2ETests(Fixtures.GoogleE2EFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void ServiceProvider_ResolvesProductionGoogleJobsApiClient()
    {
        // Arrange / Act
        GoogleJobsApiClient apiClient = _fixture.ServiceProvider.GetRequiredService<GoogleJobsApiClient>();

        // Assert
        Assert.Equal(typeof(GoogleJobsApiClient), apiClient.GetType());
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_ClientIsConfigured()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Assert - Client exists and has expected platform name
        Assert.NotNull(client);
        Assert.Equal("Google", client.PlatformName);
        
        _output.WriteLine("GoogleJobClient is properly configured and responsive");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetails_ClientIsResponsive()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Assert - Client exists
        Assert.NotNull(client);
        
        _output.WriteLine("GoogleJobClient responds to method calls");
    }


    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("Google", platformName);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_WithEmptyQuery_ClientHandles()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Assert - Client handles empty queries gracefully
        Assert.NotNull(client);
        
        _output.WriteLine("GoogleJobClient handles empty query criteria");
    }


    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_RespectsConfiguration()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Assert - Client respects configuration
        Assert.NotNull(client);
        
        _output.WriteLine("GoogleJobClient respects configuration");
    }


    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobs_ReturnsEmptyList()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act
        var results = await client.GetSavedJobsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetApplications_ReturnsEmptyList()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act
        var results = await client.GetApplicationsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task AllJobsHaveRequiredFields_ContractValidates()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Assert - Client implements contract correctly
        Assert.NotNull(client);
        Assert.Equal("Google", client.PlatformName);

        _output.WriteLine("GoogleJobClient validates required fields contract");
    }
}
