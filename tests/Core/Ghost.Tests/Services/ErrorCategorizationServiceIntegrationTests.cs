using System.Net;
using System.Net.Http;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Core.Services;
using Xunit;

namespace Ghost.Core.Tests.Services;

/// <summary>
/// Integration tests for ErrorCategorizationService covering error categorization.
/// </summary>
public class ErrorCategorizationServiceIntegrationTests
{
    [Fact]
    public void CategorizeError_NetworkError_ReturnsNetworkCategory()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Network");
        result.Message.Should().Be("Network error: Network error");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Verify internet connection and try again");
    }

    [Fact]
    public void CategorizeError_TimeoutError_ReturnsTimeoutCategory()
    {
        // Arrange
        var exception = new TaskCanceledException("Request timed out");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Timeout");
        result.Message.Should().Be("Request timed out");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Increase timeout settings or try with browser fallback");
    }

    [Fact]
    public void CategorizeError_AuthError_ReturnsAuthCategory()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Authentication failed");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Authentication failed");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeError_ConfigurationError_ReturnsConfigurationCategory()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("Configuration");
        result.Message.Should().Be("Invalid argument: Invalid argument");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check configuration settings and parameters");
    }

    [Fact]
    public void CategorizeError_ParseError_ReturnsParseCategory()
    {
        // Arrange
        var exception = new InvalidOperationException("Parse error");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Parse");
        result.Message.Should().Be("Parse error: Parse error");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("The website structure may have changed, parser needs updating");
    }

    [Fact]
    public void CategorizeError_CancelledError_ReturnsCancelledCategory()
    {
        // Arrange
        var exception = new OperationCanceledException("Request was cancelled");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Cancelled");
        result.Message.Should().Be("Request was cancelled");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Request was cancelled by user or timeout");
    }

    [Fact]
    public void CategorizeError_UnknownError_ReturnsUnknownCategory()
    {
        // Arrange
        var exception = new Exception("Unknown error");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Unknown");
        result.Message.Should().Be("Unknown error");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpError_Unauthorized_ReturnsAuthCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Authentication required");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeHttpError_Forbidden_ReturnsAuthCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Auth");
        result.Message.Should().Be("Access forbidden");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check API credentials and authentication tokens");
    }

    [Fact]
    public void CategorizeHttpError_NotFound_ReturnsNotFoundCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("NotFound");
        result.Message.Should().Be("Resource not found");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpError_TooManyRequests_ReturnsRateLimitCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("RateLimit");
        result.Message.Should().Be("Rate limit exceeded");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("Wait before retrying or reduce request frequency");
    }

    [Fact]
    public void CategorizeHttpError_InternalServerError_ReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Internal server error");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpError_BadGateway_ReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Bad gateway");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpError_ServiceUnavailable_ReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "LinkedIn");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("LinkedIn");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Service unavailable");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpError_GatewayTimeout_ReturnsServerCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.GatewayTimeout);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Indeed");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Indeed");
        result.ErrorCategory.Should().Be("Server");
        result.Message.Should().Be("Gateway timeout");
        result.Retryable.Should().BeTrue();
        result.Suggestion.Should().Be("The service is temporarily unavailable, try again later");
    }

    [Fact]
    public void CategorizeHttpError_BadRequest_ReturnsClientCategory()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Client");
        result.Message.Should().Be("HTTP error: BadRequest");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeHttpError_UnknownStatusCode_ReturnsUnknownCategory()
    {
        // Arrange
        var response = new HttpResponseMessage((HttpStatusCode)418);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Glassdoor");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Glassdoor");
        result.ErrorCategory.Should().Be("Client");
        result.Message.Should().Be("HTTP error: 418");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check logs for more details and try again");
    }

    [Fact]
    public void CategorizeError_SetsTimestamp()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        var before = DateTime.UtcNow;

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");
        var after = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void CategorizeHttpError_SetsTimestamp()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var before = DateTime.UtcNow;

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");
        var after = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void CategorizeError_IncludesTechnicalDetails()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("HttpRequestException");
        result.TechnicalDetails.Should().Contain("Network error");
    }

    [Fact]
    public void CategorizeHttpError_IncludesTechnicalDetails()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("500");
        result.TechnicalDetails.Should().Contain("Internal Server Error");
    }

    [Fact]
    public void CategorizeError_HandlesInnerException()
    {
        // Arrange
        var innerException = new Exception("Inner error");
        var exception = new HttpRequestException("Network error", innerException);

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.TechnicalDetails.Should().NotBeNull();
        result.TechnicalDetails.Should().Contain("HttpRequestException");
        result.TechnicalDetails.Should().Contain("Inner error");
    }

    [Fact]
    public void CategorizeError_HandlesArgumentNullException()
    {
        // Arrange
        var exception = new ArgumentNullException("param", "Parameter cannot be null");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Configuration");
        result.Message.Should().Be("Invalid argument: Parameter cannot be null (Parameter 'param')");
        result.Retryable.Should().BeFalse();
        result.Suggestion.Should().Be("Check configuration settings and parameters");
    }

    [Fact]
    public void CategorizeError_HandlesComplexExceptionMessage()
    {
        // Arrange
        var exception = new HttpRequestException("A complex error message with details: timeout after 30 seconds");

        // Act
        var result = ErrorCategorizationService.CategorizeError(exception, "Google");

        // Assert
        result.Should().NotBeNull();
        result.Platform.Should().Be("Google");
        result.ErrorCategory.Should().Be("Network");
        result.Message.Should().Be("Network error: A complex error message with details: timeout after 30 seconds");
        result.Retryable.Should().BeTrue();
    }

    [Fact]
    public void CategorizeHttpError_HandlesCustomReasonPhrase()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "Custom Error Message"
        };

        // Act
        var result = ErrorCategorizationService.CategorizeHttpError(response, "Google");

        // Assert
        result.TechnicalDetails.Should().Contain("Custom Error Message");
    }
}
