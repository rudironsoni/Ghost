using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Ghost.Resilience;
using Xunit;

namespace Ghost.Tests.Resilience;

public class RetryableErrorClassifierTests
{
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void IsRetryable_StatusCodes_AreRetryable(HttpStatusCode code)
    {
        RetryableErrorClassifier.IsRetryable(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void IsRetryable_StatusCodes_AreNotRetryable(HttpStatusCode code)
    {
        RetryableErrorClassifier.IsRetryable(code).Should().BeFalse();
    }

    [Fact]
    public void IsRetryable_TaskCanceledException_IsRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void IsRetryable_HttpRequestException_IsRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new HttpRequestException("network")).Should().BeTrue();
    }

    [Fact]
    public void IsRetryable_ValidationException_IsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new ValidationException("invalid")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryable_JsonException_IsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new JsonException("parse")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryable_InvalidOperationException_IsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new InvalidOperationException("parse")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryable_UnknownException_IsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new Exception("other")).Should().BeFalse();
    }
}
