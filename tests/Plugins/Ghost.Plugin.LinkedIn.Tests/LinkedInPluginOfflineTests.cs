using FluentAssertions;
using Ghost.Plugin.LinkedIn.Tests.Fixtures;
using Ghost.Testing.Reliability;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

[Trait("Category", "Integration")]
[Trait("Capability", "RequiresMockServer")]
public class LinkedInPluginOfflineTests : IClassFixture<LinkedInWireMockFixture>
{
    private readonly LinkedInWireMockFixture _fixture;

    public LinkedInPluginOfflineTests(LinkedInWireMockFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [TestTimeout(30000)]
    public async Task SearchFixture_ShouldReturnDeterministicMockPayload()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };

        using var response = await client.PostAsync("/linkedin/jobs/search", new StringContent("{}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        body.Should().Contain("li-1");
        body.Should().Contain("Software Engineer");
    }

    [Fact]
    [TestTimeout(30000)]
    public async Task SearchFixture_Request_ShouldCompleteWithinCeiling()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var task = client.PostAsync("/linkedin/jobs/search", new StringContent("{}"), cts.Token);
        var response = await task.WaitAsync(TimeSpan.FromSeconds(10));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
