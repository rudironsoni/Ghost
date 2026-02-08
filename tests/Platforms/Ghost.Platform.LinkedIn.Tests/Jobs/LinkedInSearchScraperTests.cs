using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Internal;
using Ghost.Platform.LinkedIn.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests.Jobs;

public sealed class LinkedInSearchScraperTests : IDisposable
{
    private readonly LinkedInSessionPool _mockSessionPool;
    private readonly IBrowserSession _mockSession;
    private readonly IPage _mockPage;
    private readonly IElement _mockElement;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInSearchScraper> _logger;
    private readonly LinkedInSearchScraper _scraper;

    public LinkedInSearchScraperTests()
    {
        _mockSessionPool = Substitute.For<LinkedInSessionPool>(
            Substitute.For<IGhostKernel>(),
            new LinkedInSessionPoolOptions(),
            Substitute.For<ILogger<LinkedInSessionPool>>());

        _mockSession = Substitute.For<IBrowserSession>();
        _mockPage = Substitute.For<IPage>();
        _mockElement = Substitute.For<IElement>();

        _options = new LinkedInOptions
        {
            BaseUrl = "https://www.linkedin.com",
            PageLoadTimeout = TimeSpan.FromSeconds(30)
        };

        _logger = Substitute.For<ILogger<LinkedInSearchScraper>>();

        _scraper = new LinkedInSearchScraper(
            _mockSessionPool,
            Options.Create(_options),
            _logger);
    }

    [Fact]
    public async Task SearchJobsAsync_WithValidCriteria_ReturnsJobs()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "San Francisco, CA",
            MaxResults = 5
        };

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(_mockPage);

        _mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test content</body></html>");

        var mockNodes = new List<IElement> { _mockElement };
        _mockPage.QuerySelectorAllAsync(Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(mockNodes);

        _mockElement.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IElement?)null);

        var titleElement = Substitute.For<IElement>();
        titleElement.GetTextContentAsync(Arg.Any<CancellationToken>())
            .Returns("Software Engineer");

        var companyElement = Substitute.For<IElement>();
        companyElement.GetTextContentAsync(Arg.Any<CancellationToken>())
            .Returns("Tech Corp");

        var locationElement = Substitute.For<IElement>();
        locationElement.GetTextContentAsync(Arg.Any<CancellationToken>())
            .Returns("San Francisco, CA");

        var linkElement = Substitute.For<IElement>();
        linkElement.GetAttributeAsync("href", Arg.Any<CancellationToken>())
            .Returns("https://www.linkedin.com/jobs/view/123456");

        _mockElement.QuerySelectorAsync(".job-card-list__title, .base-search-card__title", Arg.Any<CancellationToken>())
            .Returns(titleElement);

        _mockElement.QuerySelectorAsync(".job-card-container__company-name, .base-search-card__subtitle", Arg.Any<CancellationToken>())
            .Returns(companyElement);

        _mockElement.QuerySelectorAsync(".job-card-container__metadata-item, .job-search-card__location", Arg.Any<CancellationToken>())
            .Returns(locationElement);

        _mockElement.QuerySelectorAsync("a.base-card__full-link, a.job-card-list__title", Arg.Any<CancellationToken>())
            .Returns(linkElement);

        // Act
        var results = await _scraper.SearchJobsAsync(criteria, CancellationToken.None);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Software Engineer");
        results[0].Company.Should().Be("Tech Corp");
        results[0].Location.Should().Be("San Francisco, CA");
        results[0].Source.Should().Be("LinkedIn");

        _mockSessionPool.Received(1).Release(_mockSession);
    }

    [Fact]
    public async Task SearchJobsAsync_WithNullCriteria_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _scraper.SearchJobsAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SearchJobsAsync_AppliesRateLimit_BetweenRequests()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "Remote",
            MaxResults = 1
        };

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(_mockPage);

        _mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test</body></html>");

        _mockPage.QuerySelectorAllAsync(Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(new List<IElement>());

        var start = DateTime.UtcNow;

        // Act
        await _scraper.SearchJobsAsync(criteria, CancellationToken.None);
        await _scraper.SearchJobsAsync(criteria, CancellationToken.None);

        var elapsed = DateTime.UtcNow - start;

        // Assert
        // Should take at least 2 seconds (rate limit) between requests
        elapsed.TotalSeconds.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void PlatformName_ReturnsLinkedIn()
    {
        // Act
        var platformName = _scraper.PlatformName;

        // Assert
        platformName.Should().Be("LinkedIn");
    }

    [Fact]
    public async Task SearchJobsAsync_ReleasesSession_OnException()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            MaxResults = 5
        };

        _mockSessionPool.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_mockSession);

        _mockSession.NewPageAsync(Arg.Any<PageOptions>(), ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IPage>(new InvalidOperationException("Test exception")));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _scraper.SearchJobsAsync(criteria, CancellationToken.None));

        _mockSessionPool.Received(1).Release(_mockSession);
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
