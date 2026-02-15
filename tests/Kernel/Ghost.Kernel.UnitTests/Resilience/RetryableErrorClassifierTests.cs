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
    public void IsRetryableStatusCodesAreRetryable(HttpStatusCode code)
    {
        RetryableErrorClassifier.IsRetryable(code).Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void IsRetryableStatusCodesAreNotRetryable(HttpStatusCode code)
    {
        RetryableErrorClassifier.IsRetryable(code).Should().BeFalse();
    }

    [Fact]
    public void IsRetryableTaskCanceledExceptionIsRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void IsRetryableHttpRequestExceptionIsRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new HttpRequestException("network")).Should().BeTrue();
    }

    [Fact]
    public void IsRetryableValidationExceptionIsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new ValidationException("invalid")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryableJsonExceptionIsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new JsonException("parse")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryableInvalidOperationExceptionIsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new InvalidOperationException("parse")).Should().BeFalse();
    }

    [Fact]
    public void IsRetryableUnknownExceptionIsNotRetryable()
    {
        RetryableErrorClassifier.IsRetryable(new NotSupportedException("other")).Should().BeFalse();
    }
}
