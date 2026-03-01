using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Resilience;
using Ghost.Services;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class ResilientJobScraperTests : ReliabilityTestBase
{
    public ResilientJobScraperTests(ITestOutputHelper output) : base(output)
    {
    }

    private static ResilientJobScraper CreateScraper(
        IJobScraper? innerScraper = null,
        ICircuitBreaker? circuitBreaker = null,
        IGenericDeadLetterStore? deadLetterQueue = null)
    {
        return new ResilientJobScraper(
            innerScraper ?? Mock.Of<IJobScraper>(),
            circuitBreaker ?? Mock.Of<ICircuitBreaker>(),
            deadLetterQueue ?? Mock.Of<IGenericDeadLetterStore>(),
            NullLogger<ResilientJobScraper>.Instance);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullInnerScraper_ThrowsArgumentNullException()
    {
        Action act = () =>
        {
            _ = new ResilientJobScraper(
                null!,
                Mock.Of<ICircuitBreaker>(),
                Mock.Of<IGenericDeadLetterStore>(),
                NullLogger<ResilientJobScraper>.Instance);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("innerScraper");
    }

    [Fact]
    public void Constructor_NullCircuitBreaker_ThrowsArgumentNullException()
    {
        Action act = () =>
        {
            _ = new ResilientJobScraper(
                Mock.Of<IJobScraper>(),
                null!,
                Mock.Of<IGenericDeadLetterStore>(),
                NullLogger<ResilientJobScraper>.Instance);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("circuitBreaker");
    }

    [Fact]
    public void Constructor_NullDeadLetterQueue_ThrowsArgumentNullException()
    {
        Action act = () =>
        {
            _ = new ResilientJobScraper(
                Mock.Of<IJobScraper>(),
                Mock.Of<ICircuitBreaker>(),
                null!,
                NullLogger<ResilientJobScraper>.Instance);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("deadLetterQueue");
    }

    [Fact]
    public void Constructor_NullLogger_UsesNullLogger()
    {
        var scraper = new ResilientJobScraper(
            Mock.Of<IJobScraper>(),
            Mock.Of<ICircuitBreaker>(),
            Mock.Of<IGenericDeadLetterStore>(),
            null!);

        scraper.Should().NotBeNull();
    }

    #endregion

    #region PlatformName Tests

    [Fact]
    public void PlatformName_ReturnsInnerScraperPlatformName()
    {
        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        ResilientJobScraper scraper = CreateScraper(mockInner.Object);

        scraper.PlatformName.Should().Be("TestPlatform");
    }

    #endregion

    #region SearchJobsAsync Tests

    [Fact]
    public async Task SearchJobsAsync_CircuitBreakerSuccess_ReturnsJobs()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var expectedJobs = new List<JobListing>
        {
            new() { Id = "1", Title = "Job 1" },
            new() { Id = "2", Title = "Job 2" }
        };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.SearchJobsAsync(criteria, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .Returns<Func<Task<IReadOnlyList<JobListing>>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        IReadOnlyList<JobListing> result = await scraper.SearchJobsAsync(criteria);

        result.Should().BeEquivalentTo(expectedJobs);
        mockCircuit.Verify(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()), Times.Once);
    }

    [Fact]
    public async Task SearchJobsAsync_CircuitBreakerOpen_EnqueuesToDlqAndThrows()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var exception = new InvalidOperationException("Circuit is open");

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Open);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .ThrowsAsync(exception);

        var mockDlq = new Mock<IGenericDeadLetterStore>();

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.SearchJobsAsync(criteria);
        await act.Should().ThrowAsync<InvalidOperationException>();

        mockDlq.Verify(x => x.EnqueueAsync(
            It.IsAny<object>(),
            exception.Message,
            exception,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchJobsAsync_OperationCanceled_DoesNotEnqueueToDlq()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .ThrowsAsync(new OperationCanceledException());

        var mockDlq = new Mock<IGenericDeadLetterStore>();

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.SearchJobsAsync(criteria);
        await act.Should().ThrowAsync<OperationCanceledException>();

        mockDlq.Verify(x => x.EnqueueAsync(
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<Exception>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetJobDetailsAsync Tests

    [Fact]
    public async Task GetJobDetailsAsync_Success_ReturnsJobDetails()
    {
        var jobId = "job123";
        var expectedJob = new JobListing { Id = jobId, Title = "Software Engineer" };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.GetJobDetailsAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJob);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<JobListing>>>()))
            .Returns<Func<Task<JobListing>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        JobListing result = await scraper.GetJobDetailsAsync(jobId);

        result.Should().BeEquivalentTo(expectedJob);
    }

    [Fact]
    public async Task GetJobDetailsAsync_Failure_EnqueuesToDlq()
    {
        var jobId = "job123";
        var exception = new HttpRequestException("Network error");

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<JobListing>>>()))
            .ThrowsAsync(exception);

        var mockDlq = new Mock<IGenericDeadLetterStore>();

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.GetJobDetailsAsync(jobId);
        await act.Should().ThrowAsync<HttpRequestException>();

        mockDlq.Verify(x => x.EnqueueAsync(
            It.IsAny<object>(),
            exception.Message,
            exception,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_Success_ReturnsApplication()
    {
        var jobId = "job123";
        var details = new ApplicationDetails { ApplicantName = "John Doe", ApplicantEmail = "john@example.com" };
        var expectedApplication = new JobApplication { Id = "app789", JobId = jobId };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.ApplyAsync(jobId, details, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedApplication);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<JobApplication>>>()))
            .Returns<Func<Task<JobApplication>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        JobApplication result = await scraper.ApplyAsync(jobId, details);

        result.Should().BeEquivalentTo(expectedApplication);
    }

    [Fact]
    public async Task ApplyAsync_AuthenticationException_EnqueuesToDlq()
    {
        var jobId = "job123";
        var details = new ApplicationDetails();
        var exception = new UnauthorizedAccessException("Authentication failed");

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<JobApplication>>>()))
            .ThrowsAsync(exception);

        var mockDlq = new Mock<IGenericDeadLetterStore>();

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.ApplyAsync(jobId, details);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        mockDlq.Verify(x => x.EnqueueAsync(
            It.IsAny<object>(),
            exception.Message,
            exception,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetApplicationsAsync Tests

    [Fact]
    public async Task GetApplicationsAsync_WithFilter_ReturnsApplications()
    {
        var filter = new ApplicationsFilter { Status = "pending" };
        var expectedApplications = new List<JobApplication>
        {
            new() { Id = "app1", Status = "pending" },
            new() { Id = "app2", Status = "pending" }
        };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.GetApplicationsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedApplications);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobApplication>>>>()))
            .Returns<Func<Task<IReadOnlyList<JobApplication>>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        IReadOnlyList<JobApplication> result = await scraper.GetApplicationsAsync(filter);

        result.Should().BeEquivalentTo(expectedApplications);
    }

    [Fact]
    public async Task GetApplicationsAsync_NullFilter_ReturnsAllApplications()
    {
        var expectedApplications = new List<JobApplication>
        {
            new() { Id = "app1" },
            new() { Id = "app2" }
        };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.GetApplicationsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedApplications);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobApplication>>>>()))
            .Returns<Func<Task<IReadOnlyList<JobApplication>>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        IReadOnlyList<JobApplication> result = await scraper.GetApplicationsAsync(null);

        result.Should().BeEquivalentTo(expectedApplications);
    }

    #endregion

    #region SaveJobAsync Tests

    [Fact]
    public async Task SaveJobAsync_Success_CompletesWithoutException()
    {
        var jobId = "job123";

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.SaveJobAsync(jobId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<bool>>>()))
            .Returns<Func<Task<bool>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);

        Func<Task> act = async () => await scraper.SaveJobAsync(jobId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveJobAsync_Failure_EnqueuesToDlq()
    {
        var jobId = "job123";
        var exception = new InvalidOperationException("Save failed");

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<bool>>>()))
            .ThrowsAsync(exception);

        var mockDlq = new Mock<IGenericDeadLetterStore>();

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.SaveJobAsync(jobId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        mockDlq.Verify(x => x.EnqueueAsync(
            It.IsAny<object>(),
            exception.Message,
            exception,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetSavedJobsAsync Tests

    [Fact]
    public async Task GetSavedJobsAsync_Success_ReturnsSavedJobs()
    {
        var expectedJobs = new List<JobListing>
        {
            new() { Id = "job1", Title = "Job 1" },
            new() { Id = "job2", Title = "Job 2" }
        };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.GetSavedJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .Returns<Func<Task<IReadOnlyList<JobListing>>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        IReadOnlyList<JobListing> result = await scraper.GetSavedJobsAsync();

        result.Should().BeEquivalentTo(expectedJobs);
    }

    #endregion

    #region DLQ Error Handling Tests

    [Fact]
    public async Task CircuitBreakerThrows_DlqEnqueueThrows_LogsErrorButPropagatesOriginal()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var originalException = new HttpRequestException("Original error");
        var dlqException = new InvalidOperationException("DLQ failed");

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .ThrowsAsync(originalException);

        var mockDlq = new Mock<IGenericDeadLetterStore>();
        mockDlq.Setup(x => x.EnqueueAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<Exception>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(dlqException);

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object, mockDlq.Object);

        Func<Task> act = async () => await scraper.SearchJobsAsync(criteria);
        ExceptionAssertions<HttpRequestException> thrown = await act.Should().ThrowAsync<HttpRequestException>();
        thrown.Which.Message.Should().Be("Original error");
    }

    [Fact]
    public async Task CircuitBreakerHalfOpen_Success_ReturnsResult()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var expectedJobs = new List<JobListing> { new() { Id = "1", Title = "Job 1" } };

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.SearchJobsAsync(criteria, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.HalfOpen);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .Returns<Func<Task<IReadOnlyList<JobListing>>>>(f => f());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);
        IReadOnlyList<JobListing> result = await scraper.SearchJobsAsync(criteria);

        result.Should().BeEquivalentTo(expectedJobs);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SearchJobsAsync_CancellationRequested_PropagatesCancellation()
    {
        var criteria = new JobSearchCriteria { Query = "developer" };
        var cts = new CancellationTokenSource();

        var mockInner = new Mock<IJobScraper>();
        mockInner.Setup(x => x.PlatformName).Returns("TestPlatform");

        var mockCircuit = new Mock<ICircuitBreaker>();
        mockCircuit.Setup(x => x.State).Returns(CircuitState.Closed);
        mockCircuit.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task<IReadOnlyList<JobListing>>>>()))
            .ThrowsAsync(new OperationCanceledException());

        ResilientJobScraper scraper = CreateScraper(mockInner.Object, mockCircuit.Object);

        Func<Task> act = async () => await scraper.SearchJobsAsync(criteria, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
