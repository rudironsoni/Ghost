using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Kernel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Kernel.Tests.Services;

/// <summary>
/// Integration tests for AggregatedJobClient covering multi-platform job search, error handling, and deduplication.
/// </summary>
public class AggregatedJobClientIntegrationTests : ReliabilityTestBase
{
    private readonly Mock<ILogger<AggregatedJobClient>> _mockLogger;
    private readonly Mock<IDeduplicationService> _mockDedupe;

    public AggregatedJobClientIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _mockLogger = new Mock<ILogger<AggregatedJobClient>>();
        _mockDedupe = new Mock<IDeduplicationService>();
        _mockDedupe.Setup(d => d.GenerateId(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((title, company) => $"{title}-{company}");
    }

    [Fact]
    public async Task SearchJobsAsyncReturnsJobsWhenAllScrapersSucceed()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().Contain(j => j.Title == "Software Engineer");
        result.Should().Contain(j => j.Title == "Data Scientist");
    }

    [Fact]
    public async Task SearchJobsAsyncDeduplicatesJobsWhenSameJobFromMultipleSources()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Software Engineer", Company = "Company A", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result[0].Title.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task SearchJobsAsyncReturnsEmptyWhenAllScrapersFail()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsyncReturnsPartialResultsWhenSomeScrapersFail()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result[0].Title.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task SearchJobsAsyncFiltersBySourcesWhenSourcesSpecified()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "Google" }
        };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result[0].Source.Should().Be("Google");
        mockScraper1.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
        mockScraper2.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchJobsAsyncRunsAllScrapersWhenNoSourcesSpecified()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(2);
        mockScraper1.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
        mockScraper2.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchJobsAsyncHandlesEmptyScraperList()
    {
        // Arrange
        var client = new AggregatedJobClient(Enumerable.Empty<IJobScraper>(), _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsyncHandlesNullCriteria()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>());

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchJobsAsyncRespectsCancellationToken()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        cts.Cancel();
        Func<Task> act = async () => await client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchJobsAsyncLogsScraperFailures()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var testLogger = new TestLogger<AggregatedJobClient>();
        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, testLogger);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        await client.SearchJobsAsync(criteria);

        // Assert
        testLogger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Warning);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                State = state?.ToString(),
                Exception = exception
            });
        }
    }

    private sealed class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public string? State { get; set; }
        public Exception? Exception { get; set; }
    }

    [Fact]
    public async Task SearchJobsAsyncRunsScrapersInParallel()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Returns(Task.Run(async () =>
            {
                await Task.Delay(200);
                return new List<JobListing>
                {
                    new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
                } as IReadOnlyList<JobListing>;
            }));

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Returns(Task.Run(async () =>
            {
                await Task.Delay(200);
                return new List<JobListing>
                {
                    new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
                } as IReadOnlyList<JobListing>;
            }));

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        DateTime startTime = DateTime.UtcNow;
        await client.SearchJobsAsync(criteria);
        DateTime endTime = DateTime.UtcNow;

        // Assert
        (endTime - startTime).TotalMilliseconds.Should().BeLessThan(1100);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsyncReturnsStructuredErrorsWhenScrapersFail()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        JobSearchResult result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Jobs.Count.Should().Be(1);
        result.PlatformErrors.Count.Should().Be(1);
        result.PlatformErrors[0].Platform.Should().Be("Google");
        result.PlatformErrors[0].ErrorCategory.Should().Be("Network");
        result.Metadata.Should().NotBeNull();
        result.Metadata.TotalPlatforms.Should().Be(2);
        result.Metadata.SuccessfulPlatforms.Should().Be(1);
        result.Metadata.FailedPlatforms.Should().Be(1);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsyncReturnsUnsuccessfulWhenAllScrapersFail()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        JobSearchResult result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Jobs.Should().BeEmpty();
        result.PlatformErrors.Count.Should().Be(2);
        result.ErrorMessage.Should().Be("All platforms failed to return results");
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsyncIncludesExecutionTime()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria { Query = "software engineer" };

        // Act
        JobSearchResult result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.ExecutionTimeMs.Should().BeGreaterOrEqualTo(0);
        result.Metadata.ExecutionTimeMs.Should().BeLessThan(10000);
    }

    [Fact]
    public async Task SearchJobsWithErrorsAsyncIncludesCriteriaInMetadata()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>());

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "San Francisco",
            MaxResults = 10
        };

        // Act
        JobSearchResult result = await client.SearchJobsWithErrorsAsync(criteria);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Criteria.Should().NotBeNull();
        result.Metadata.Criteria!.Query.Should().Be("software engineer");
        result.Metadata.Criteria.Location.Should().Be("San Francisco");
        result.Metadata.Criteria.MaxResults.Should().Be(10);
    }

    [Fact]
    public async Task GetJobDetailsAsyncReturnsDetailsWhenScraperSucceeds()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.GetJobDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobListing
            {
                Id = "1",
                Title = "Software Engineer",
                Company = "Company A",
                Description = "A great job opportunity"
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);

        // Act
        JobListing result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Software Engineer");
        result.Description.Should().Be("A great job opportunity");
    }

    [Fact]
    public async Task GetJobDetailsAsyncTriesMultipleScrapersUntilOneSucceeds()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.GetJobDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobListing { Id = "1", Title = "" });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.GetJobDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobListing
            {
                Id = "1",
                Title = "Software Engineer",
                Company = "Company A"
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);

        // Act
        JobListing result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Software Engineer");
        mockScraper1.Verify(s => s.GetJobDetailsAsync("1", It.IsAny<CancellationToken>()), Times.Once);
        mockScraper2.Verify(s => s.GetJobDetailsAsync("1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetJobDetailsAsyncReturnsEmptyJobWhenAllScrapersFail()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.GetJobDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new AggregatedJobClient(new[] { mockScraper1.Object }, _mockDedupe.Object, _mockLogger.Object);

        // Act
        JobListing result = await client.GetJobDetailsAsync("1");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("1");
        result.Title.Should().BeEmpty();
    }

    [Fact]
    public void PlatformNameReturnsAggregated()
    {
        // Arrange
        var client = new AggregatedJobClient(Enumerable.Empty<IJobScraper>(), _mockDedupe.Object, _mockLogger.Object);

        // Act
        string platformName = client.PlatformName;

        // Assert
        platformName.Should().Be("Aggregated");
    }

    [Fact]
    public async Task SearchJobsAsyncHandlesCaseInsensitiveSourceFiltering()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "google" }
        };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(1);
        result[0].Source.Should().Be("Google");
        mockScraper1.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
        mockScraper2.Verify(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchJobsAsyncHandlesMultipleSources()
    {
        // Arrange
        var mockScraper1 = new Mock<IJobScraper>();
        mockScraper1.Setup(s => s.PlatformName).Returns("Google");
        mockScraper1.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "1", Title = "Software Engineer", Company = "Company A", Source = "Google" }
            });

        var mockScraper2 = new Mock<IJobScraper>();
        mockScraper2.Setup(s => s.PlatformName).Returns("Glassdoor");
        mockScraper2.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "2", Title = "Data Scientist", Company = "Company B", Source = "Glassdoor" }
            });

        var mockScraper3 = new Mock<IJobScraper>();
        mockScraper3.Setup(s => s.PlatformName).Returns("LinkedIn");
        mockScraper3.Setup(s => s.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>
            {
                new() { Id = "3", Title = "Product Manager", Company = "Company C", Source = "LinkedIn" }
            });

        var client = new AggregatedJobClient(new[] { mockScraper1.Object, mockScraper2.Object, mockScraper3.Object }, _mockDedupe.Object, _mockLogger.Object);
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Sources = new List<string> { "Google", "Glassdoor" }
        };

        // Act
        IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria);

        // Assert
        result.Count.Should().Be(2);
        result.Should().Contain(j => j.Source == "Google");
        result.Should().Contain(j => j.Source == "Glassdoor");
        result.Should().NotContain(j => j.Source == "LinkedIn");
    }
}
