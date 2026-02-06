using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Ghost.Core.Tests.Services;

/// <summary>
/// Integration tests for AggregatedJobClient covering multi-platform job search, error handling, and deduplication.
/// </summary>
public class AggregatedJobClientIntegrationTests
{
    private readonly ILogger<AggregatedJobClient> _logger;
    private readonly IDeduplicationService _dedupe;

    public AggregatedJobClientIntegrationTests()
    {
        _logger = Substitute.For<ILogger<AggregatedJobClient>>();
        _dedupe = Substitute.For<IDeduplicationService>();
        _dedupe.GenerateId(Arg.Any<string>(), Arg.Any<string>()).Returns(x => $"{x.ArgAt<string>(0)}-{x.ArgAt<string>(1)}");
    }

    [Fact]
    public async Task SearchJobsAsync_ReturnsJobs_WhenAllScrapersSucceed()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().Contain(j => j.Title == "Software Engineer");
        result.Should().Contain(j => j.Title == "Data Scientist");
    }

    [Fact]
    public async Task SearchJobsAsync_DeduplicatesJobs_WhenSameJobFromMultipleSources()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Software Engineer", Company = "Company A", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.First().Title.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task SearchJobsAsync_ReturnsEmpty_WhenAllScrapersFail()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsync_ReturnsPartialResults_WhenSomeScrapersFail()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.First().Title.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task SearchJobsAsync_FiltersBySources_WhenSourcesSpecified()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "Google" }
        };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.First().Source.Should().Be("Google");
        await scraper1.Received(1).SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
        await scraper2.DidNotReceive().SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchJobsAsync_RunsAllScrapers_WhenNoSourcesSpecified()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(2);
        await scraper1.Received(1).SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
        await scraper2.Received(1).SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchJobsAsync_HandlesEmptyScraperList()
    {
        // Arrange
        var client = new AggregatedJobClient(Enumerable.Empty<IJobScraper>(), _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsync_HandlesNullCriteria()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>());

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);

        // Act
        var result = await client.SearchJobsAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsync_RespectsCancellationToken()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new OperationCanceledException()));

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);
        var cts = new CancellationTokenSource();
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        cts.Cancel();
        Func<Task> act = async () => await client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchJobsAsync_LogsScraperFailures()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        await client.SearchJobsAsync(criteria);

        // Assert
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SearchJobsAsync_RunsScrapersInParallel()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.Run(async () =>
            {
                await Task.Delay(200);
                return new List<JobListing>
                {
                    new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
                } as IReadOnlyList<JobListing>;
            }));

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.Run(async () =>
            {
                await Task.Delay(200);
                return new List<JobListing>
                {
                    new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
                } as IReadOnlyList<JobListing>;
            }));

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var startTime = DateTime.UtcNow;
        await client.SearchJobsAsync(criteria);
        var endTime = DateTime.UtcNow;

        // Assert
        (endTime - startTime).TotalMilliseconds.Should().BeLessThan(1100);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsync_ReturnsStructuredErrors_WhenScrapersFail()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Jobs.Count.Should().Be(1);
        result.PlatformErrors.Count.Should().Be(1);
        result.PlatformErrors.First().Platform.Should().Be("Google");
        result.PlatformErrors.First().ErrorCategory.Should().Be("Network");
        result.Metadata.Should().NotBeNull();
        result.Metadata.TotalPlatforms.Should().Be(2);
        result.Metadata.SuccessfulPlatforms.Should().Be(1);
        result.Metadata.FailedPlatforms.Should().Be(1);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsync_ReturnsUnsuccessful_WhenAllScrapersFail()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<JobListing>>(new HttpRequestException("Network error")));

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Jobs.Should().BeEmpty();
        result.PlatformErrors.Count.Should().Be(2);
        result.ErrorMessage.Should().Be("All platforms failed to return results");
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsync_IncludesExecutionTime()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        var result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.ExecutionTimeMs.Should().BeGreaterOrEqualTo(0);
        result.Metadata.ExecutionTimeMs.Should().BeLessThan(10000);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsync_IncludesCriteriaInMetadata()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>());

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "San Francisco",
            MaxResults = 10
        };

        // Act
        var result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Criteria.Should().NotBeNull();
        result.Metadata.Criteria!.Query.Should().Be("software engineer");
        result.Metadata.Criteria.Location.Should().Be("San Francisco");
        result.Metadata.Criteria.MaxResults.Should().Be(10);
    }

    [Fact]
    public async Task GetJobDetailsAsync_ReturnsDetails_WhenScraperSucceeds()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.GetJobDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new JobListing
            {
                Id = "1",
                Title = "Software Engineer",
                Company = "Company A",
                Description = "A great job opportunity"
            });

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);

        // Act
        var result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Software Engineer");
        result.Description.Should().Be("A great job opportunity");
    }

    [Fact]
    public async Task GetJobDetailsAsync_TriesMultipleScrapers_UntilOneSucceeds()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.GetJobDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new JobListing { Id = "1", Title = "" });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.GetJobDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new JobListing
            {
                Id = "1",
                Title = "Software Engineer",
                Company = "Company A"
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);

        // Act
        var result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Software Engineer");
        await scraper1.Received(1).GetJobDetailsAsync("1", Arg.Any<CancellationToken>());
        await scraper2.Received(1).GetJobDetailsAsync("1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJobDetailsAsync_ReturnsEmptyJob_WhenAllScrapersFail()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.GetJobDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JobListing>(new HttpRequestException("Network error")));

        var client = new AggregatedJobClient(new[] { scraper1 }, _dedupe, _logger);

        // Act
        var result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("1");
        result.Title.Should().BeEmpty();
    }

    [Fact]
    public void PlatformName_ReturnsAggregated()
    {
        // Arrange
        var client = new AggregatedJobClient(Enumerable.Empty<IJobScraper>(), _dedupe, _logger);

        // Act
        var platformName = client.PlatformName;

        // Assert
        platformName.Should().Be("Aggregated");
    }

    [Fact]
    public async Task SearchJobsAsync_HandlesCaseInsensitiveSourceFiltering()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "google" }
        };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(1);
        result.First().Source.Should().Be("Google");
        await scraper1.Received(1).SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
        await scraper2.DidNotReceive().SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchJobsAsync_HandlesMultipleSources()
    {
        // Arrange
        var scraper1 = Substitute.For<IJobScraper>();
        scraper1.PlatformName.Returns("Google");
        scraper1.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var scraper2 = Substitute.For<IJobScraper>();
        scraper2.PlatformName.Returns("Glassdoor");
        scraper2.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var scraper3 = Substitute.For<IJobScraper>();
        scraper3.PlatformName.Returns("LinkedIn");
        scraper3.SearchJobsAsync(Arg.Any<JobSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobListing>
            {
                new() { Id = "3", Title = "Product Manager", Company = "Company C", Source = "LinkedIn" }
            });

        var client = new AggregatedJobClient(new[] { scraper1, scraper2, scraper3 }, _dedupe, _logger);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "Google", "Glassdoor" }
        };

        // Act
        var result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(2);
        result.Should().Contain(j => j.Source == "Google");
        result.Should().Contain(j => j.Source == "Glassdoor");
        result.Should().NotContain(j => j.Source == "LinkedIn");
    }
}
