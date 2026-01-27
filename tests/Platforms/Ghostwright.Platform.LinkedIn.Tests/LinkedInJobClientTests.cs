using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.LinkedIn.Tests
{
    public class LinkedInJobClientTests
    {
        [Fact]
        public async Task SearchJobsAsync_ReturnsEnumerable()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));
            mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IElement?>(null));

            var client = new LinkedInJobClient(mockSession, new LinkedInOptions());
            var jobs = await client.SearchJobsAsync("developer", CancellationToken.None);
            jobs.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Job>>();
        }

        [Fact]
        public async Task ApplyAsync_ReturnsFalse_WhenNoApplyButton()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));
            mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IElement?>(null));

            var client = new LinkedInJobClient(mockSession, new LinkedInOptions());
            var result = await client.ApplyAsync("job:1", CancellationToken.None);
            result.Should().BeFalse();
        }
    }
}
