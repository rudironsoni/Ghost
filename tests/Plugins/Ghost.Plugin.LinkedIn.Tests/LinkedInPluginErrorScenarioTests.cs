using FluentAssertions;
using Ghost.Plugin.LinkedIn.Tests.Fixtures;
using Ghost.Testing.Reliability;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

[Trait("Category", "Integration")]
[Trait("Capability", "RequiresMockServer")]
public class LinkedInPluginErrorScenarioTests : IClassFixture<LinkedInWireMockFixture>
{
    private readonly LinkedInWireMockFixture _fixture;

    public LinkedInPluginErrorScenarioTests(LinkedInWireMockFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/linkedin/jobs/rate-limited", System.Net.HttpStatusCode.TooManyRequests)]
    [InlineData("/linkedin/jobs/unauthorized", System.Net.HttpStatusCode.Unauthorized)]
    [InlineData("/linkedin/jobs/error", System.Net.HttpStatusCode.InternalServerError)]
    [TestTimeout(30000)]
    public async Task ErrorEndpoints_ShouldReturnExpectedStatusCodes(string path, System.Net.HttpStatusCode expected)
    {
        using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    [TestTimeout(30000)]
    public async Task MalformedEndpoint_ShouldReturnInvalidJsonPayload_ForParserErrorCoverage()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };

        using var response = await client.GetAsync("/linkedin/jobs/malformed");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        body.Should().Be("{not-json}");
    }
}
