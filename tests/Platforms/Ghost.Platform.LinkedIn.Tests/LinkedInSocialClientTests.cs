using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Ghost.Contracts.Social;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInSocialClientTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsDefault_WhenNoElementFound()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInSocialClient>>();
        var client = new LinkedInSocialClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var profile = await client.GetProfileAsync("urn:li:person:123", CancellationToken.None);
        profile.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchProfilesAsync_ReturnsList_OnSuccess()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInSocialClient>>();
        var client = new LinkedInSocialClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var results = await client.SearchProfilesAsync(new ProfileSearchCriteria { Query = "engineer" }, CancellationToken.None);
        results.Should().BeAssignableTo<IEnumerable<SocialProfile>>();
    }
}
