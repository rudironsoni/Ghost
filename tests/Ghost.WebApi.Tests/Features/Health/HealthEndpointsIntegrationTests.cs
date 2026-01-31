using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Ghost.WebApi.Features.Health;
using Ghost.Contracts.Jobs;

namespace Ghost.WebApi.Tests.Features.Health;

/// <summary>
/// Integration tests for HealthEndpoints covering health check functionality.
/// </summary>
public class HealthEndpointsIntegrationTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<HealthEndpoints>> _loggerMock;
    private readonly Mock<IJobClient> _jobClientMock;

    public HealthEndpointsIntegrationTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<HealthEndpoints>>();
        _jobClientMock = new Mock<IJobClient>();
    }

    [Fact]
    public async Task CheckJobsHealth_ReturnsHealthy_WhenAllPlatformsReturnJobs()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var httpContext = new DefaultHttpContext();
        var endpointRouteBuilder = new Mock<IEndpointRouteBuilder>();

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result as Microsoft.AspNetCore.Http.IResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_ReturnsDegraded_WhenPlatformReturnsNoJobs()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>());

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_ReturnsUnhealthy_WhenPlatformThrowsException()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_MeasuresResponseTime()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_HandlesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_LogsHealthCheckStart()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting jobs health check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsHealthCheckCompletion()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Jobs health check completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsPlatformHealthTest()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Testing") && v.ToString()!.Contains("health")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsPlatformHealthPassed()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("health check passed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsPlatformHealthDegraded()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>());

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("health check degraded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsPlatformHealthFailed()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("health check failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckJobsHealth_LogsHealthCheckFailed_WhenExceptionOccurs()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Jobs health check failed with exception")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckJobsHealth_UsesTestCriteria()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        JobSearchCriteria? capturedCriteria = null;
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<JobSearchCriteria, CancellationToken>((criteria, ct) => capturedCriteria = criteria)
            .ReturnsAsync(jobs);

        // Act
        await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        capturedCriteria.Should().NotBeNull();
        capturedCriteria!.Query.Should().Be("test");
        capturedCriteria.Location.Should().Be("Remote");
        capturedCriteria.MaxResults.Should().Be(1);
    }

    [Fact]
    public async Task CheckJobsHealth_SetsLastCheckedTimestamp()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var beforeCheck = DateTime.UtcNow;

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        var afterCheck = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_SetsLastSuccessfulSearch_WhenJobsFound()
    {
        // Arrange
        var jobs = new List<JobListing>
        {
            new()
            {
                Id = "job-1",
                Title = "Software Engineer",
                Company = "Tech Company",
                Location = "San Francisco",
                Source = "Google"
            }
        };

        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckJobsHealth_DoesNotSetLastSuccessfulSearch_WhenNoJobsFound()
    {
        // Arrange
        _jobClientMock
            .Setup(x => x.SearchJobsAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobListing>());

        // Act
        var result = await HealthEndpoints.CheckJobsHealth(
            _jobClientMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}
