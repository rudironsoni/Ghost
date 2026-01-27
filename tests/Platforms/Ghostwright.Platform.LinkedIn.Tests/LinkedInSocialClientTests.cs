using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.LinkedIn.Tests
{
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

            var client = new LinkedInSocialClient(mockSession, new LinkedInOptions());
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

            var client = new LinkedInSocialClient(mockSession, new LinkedInOptions());
            var results = await client.SearchProfilesAsync("engineer", CancellationToken.None);
            results.Should().BeAssignableTo<IEnumerable<Profile>>();
        }
    }
}
