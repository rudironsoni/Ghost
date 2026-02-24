using System.Net;
using System.Net.Http;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Kernel.Services;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Tests.Services;

/// <summary>
/// Integration tests for ErrorCategorizationService covering error categorization.
/// </summary>
public class ErrorCategorizationServiceIntegrationTests : ReliabilityTestBase
{
    public ErrorCategorizationServiceIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CategorizeErrorNetworkErrorReturnsNetworkCategory()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Network");
        result.Message.Should().Be("Network error: Network error");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Verify internet connection and try again");
    }

    [Fact]
    public void CategorizeErrorTimeoutErrorReturnsTimeoutCategory()
    {
        // Arrange
        var exception = new TaskCanceledException("Request timed out");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Timeout");
        result.Message.Should().Be("Request timed out");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Increase timeout settings or try with browser fallback");
    }

    [Fact]
    public void CategorizeErrorAuthErrorReturnsAuthCategory()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Authentication failed");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Authentication failed");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeErrorConfigurationErrorReturnsConfigurationCategory()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("Configuration");
        result.Message.Should().Be("Invalid argument: Invalid argument");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check configuration settings and parameters");
    }

    [Fact]
    public void CategorizeErrorParseErrorReturnsParseCategory()
    {
        // Arrange
        var exception = new InvalidOperationException("Parse error");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Parse");
        result.Message.Should().Be("Parse error: Parse error");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("The website structure may have changed, parser needs updating");
    }

    [Fact]
    public void CategorizeErrorCancelledErrorReturnsCancelledCategory()
    {
        // Arrange
        var exception = new OperationCanceledException("Request was cancelled");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Cancelled");
        result.Message.Should().Be("Request was cancelled");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Request was cancelled by user or timeout");
    }

    [Fact]
    public void CategorizeErrorUnknownErrorReturnsUnknownCategory()
    {
        // Arrange
        var exception = new NotImplementedException("Unknown error");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Unknown");
        result.Message.Should().Be("Unknown error");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpErrorUnauthorizedReturnsAuthCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Authentication required");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeHttpErrorForbiddenReturnsAuthCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Access forbidden");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeHttpErrorNotFoundReturnsNotFoundCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("NotFound");
        result.Message.Should().Be("Resource not found");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpErrorTooManyRequestsReturnsRateLimitCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("RateLimit");
        result.Message.Should().Be("Rate limit exceeded");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Wait before retrying or reduce request frequency");
    }

    [Fact]
    public void CategorizeHttpErrorInternalServerErrorReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Internal server error");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpErrorBadGatewayReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Bad gateway");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpErrorServiceUnavailableReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Service unavailable");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpErrorGatewayTimeoutReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.GatewayTimeout);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Gateway timeout");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpErrorBadRequestReturnsClientCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Client");
        result.Message.Should().Be("HTTP error: BadRequest");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpErrorUnknownStatusCodeReturnsUnknownCategory()
    {
        // Arrange
        var response = new HttpResponseMessage((HttpStatusCode)418);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Client");
        result.Message.Should().Be("HTTP error: 418");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeErrorSetsTimestamp()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        DateTime before = DateTime.UtcNow;

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");
        DateTime after = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void CategorizeHttpErrorSetsTimestamp()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        DateTime before = DateTime.UtcNow;

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");
        DateTime after = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void CategorizeErrorIncludesTechnicalDetails()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("HttpRequestException");
        result.TechnicalDetails.Should().Contain("Network error");
    }

    [Fact]
    public void CategorizeHttpErrorIncludesTechnicalDetails()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("500");
        result.TechnicalDetails.Should().Contain("Internal Server Error");
    }

    [Fact]
    public void CategorizeErrorHandlesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var exception = new HttpRequestException("Network error", innerException);

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("HttpRequestException");
        result.TechnicalDetails.Should().Contain("Inner error");
    }

    [Fact]
    public void CategorizeErrorHandlesArgumentNullException()
    {
        // Arrange
        var exception = new ArgumentNullException("param", "Parameter cannot be null");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Configuration");
        result.Message.Should().Be("Invalid argument: Parameter cannot be null (Parameter 'param')");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check configuration settings and parameters");
    }

    [Fact]
    public void CategorizeErrorHandlesComplexExceptionMessage()
    {
        // Arrange
        var exception = new HttpRequestException("A complex error message with details: timeout after 30 seconds");

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Network");
        result.Message.Should().Be("Network error: A complex error message with details: timeout after 30 seconds");
        result.Retryable.Should().BeTrue();
    }

    [Fact]
    public void CategorizeHttpErrorHandlesCustomReasonPhrase()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "Custom Error Message"
        };

        // Act
        PlatformError result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.TechnicalDetails.Should().Contain("Custom Error Message");
    }
}
