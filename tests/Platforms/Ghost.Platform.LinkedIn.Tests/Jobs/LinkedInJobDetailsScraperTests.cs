using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Entities;
using Ghost.Platform.LinkedIn.Internal;
using Ghost.Platform.LinkedIn.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests.Jobs;

public sealed class LinkedInJobDetailsScraperTests : IDisposable
{
    private readonly LinkedInSessionPool _mockSessionPool;
    private readonly IBrowserSession _mockSession;
    private readonly IPage _mockPage;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInJobDetailsScraper> _logger;
    private readonly LinkedInJobDetailsScraper _scraper;

    public LinkedInJobDetailsScraperTests()
    {
        _mockSessionPool = Substitute.For<LinkedInSessionPool>(
            Substitute.For<IGhostKernel>(),
            new LinkedInSessionPoolOptions(),
            Substitute.For<ILogger<LinkedInSessionPool>>());

        _mockSession = Substitute.For<IBrowserSession>();
        _mockPage = Substitute.For<IPage>();

        _options = new LinkedInOptions
        {
            BaseUrl = "https://www.linkedin.com",
            PageLoadTimeout = TimeSpan.FromSeconds(30)
        };

        _logger = Substitute.For<ILogger<LinkedInJobDetailsScraper>>();

        _scraper = new LinkedInJobDetailsScraper(
            _mockSessionPool,
            Options.Create(_options),
            _logger);
    }

    [Fact]
    public async Task GetJobDetailsAsync_WithValidJobId_ReturnsJobListing()
    {
        // Arrange
        var jobId = "123456";

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(_mockPage);

        var htmlContent = @"
            <html>
                <head><title>Software Engineer at Tech Corp</title></head>
                <body>
                    <h1 class='job-title'>Software Engineer</h1>
                    <div class='company-name'>Tech Corp</div>
                    <div class='job-location'>San Francisco, CA</div>
                    <div class='job-description'>
                        We are looking for a talented Software Engineer to join our team.
                        Requirements:
                        - 5+ years of experience
                        - Proficiency in C#, .NET
                    </div>
                    <div class='job-salary'>$120k - $180k</div>
                    <span class='posted-date'>Posted 2 days ago</span>
                </body>
            </html>";

        _mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns(htmlContent);

        // Act
        var result = await _scraper.GetJobDetailsAsync(jobId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(jobId);
        result.Source.Should().Be("LinkedIn");
        result.Url.Should().Contain($"/jobs/view/{jobId}");

        _mockSessionPool.Received(1).Release(_mockSession);
    }

    [Fact]
    public async Task GetJobDetailsAsync_WithNullJobId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _scraper.GetJobDetailsAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetJobDetailsAsync_AppliesRateLimit_BetweenRequests()
    {
        // Arrange
        var jobId = "123456";

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(_mockPage);

        _mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test</body></html>");

        var start = DateTime.UtcNow;

        // Act
        await _scraper.GetJobDetailsAsync(jobId, CancellationToken.None);
        await _scraper.GetJobDetailsAsync(jobId, CancellationToken.None);

        var elapsed = DateTime.UtcNow - start;

        // Assert
        // Should take at least 2 seconds (rate limit) between requests
        elapsed.TotalSeconds.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetJobDetailsAsync_ReleasesSession_OnException()
    {
        // Arrange
        var jobId = "123456";

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IPage>(new InvalidOperationException("Test exception")));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _scraper.GetJobDetailsAsync(jobId, CancellationToken.None));

        _mockSessionPool.Received(1).Release(_mockSession);
    }

    [Fact]
    public async Task GetJobDetailsAsync_NavigatesToCorrectUrl()
    {
        // Arrange
        var jobId = "123456";

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(_mockPage);

        _mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test</body></html>");

        // Act
        await _scraper.GetJobDetailsAsync(jobId, CancellationToken.None);

        // Assert
        await _mockPage.Received(1).NavigateAsync(
            Arg.Is<string>(url => url.Contains($"/jobs/view/{jobId}")),
            Arg.Any<NavigationOptions>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Act
        _scraper.Dispose();

        // Assert - no exception should be thrown
        Assert.True(true);
    }

    public void Dispose()
    {
        _scraper?.Dispose();
    }
}
