using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInSocialClientTests
{
    [Fact]
    public async Task GetProfileAsyncReturnsDefaultWhenNoElementFound()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IElement?)null);

        var logger = new Mock<ILogger<LinkedInSocialClient>>();
        var client = new LinkedInSocialClient(mockSession.Object, Options.Create(new LinkedInOptions()), logger.Object);
        var profile = await client.GetProfileAsync("urn:li:person:123", CancellationToken.None);
        profile.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchProfilesAsyncReturnsListOnSuccess()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.QuerySelectorAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IElement>().AsReadOnly());

        var logger = new Mock<ILogger<LinkedInSocialClient>>();
        var client = new LinkedInSocialClient(mockSession.Object, Options.Create(new LinkedInOptions()), logger.Object);
        var results = await client.SearchProfilesAsync(new ProfileSearchCriteria { Query = "engineer" }, CancellationToken.None);
        results.Should().BeAssignableTo<IEnumerable<SocialProfile>>();
    }
}
