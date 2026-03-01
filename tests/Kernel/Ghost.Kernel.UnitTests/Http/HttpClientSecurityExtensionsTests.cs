using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Ghost.Http;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Unit.Tests.Http;

/// <summary>
/// Security tests for HttpClientSecurityExtensions to verify TLS certificate validation cannot be bypassed accidentally.
/// </summary>
public class HttpClientSecurityExtensionsTests : ReliabilityTestBase
{
    public HttpClientSecurityExtensionsTests(ITestOutputHelper output) : base(output) { }
    #region Configuration Tests

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithNullHandler_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithDefaults_ShouldConfigureSecureSettings()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);

        // Assert
        result.Should().BeSameAs(handler);
        result.SslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
        result.AutomaticDecompression.Should().Be(System.Net.DecompressionMethods.All);
        result.MaxAutomaticRedirections.Should().Be(10);
        result.MaxConnectionsPerServer.Should().Be(100);
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithOptions_Null_ShouldUseDefaults()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, null, null);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithOptions_DangerousBypassFalse_ShouldNotSetCallback()
    {
        // Arrange
        var handler = new HttpClientHandler();
        var options = new HttpClientSecurityOptions
        {
            DangerousAcceptAnyServerCertificate = false,
            DangerousAcceptAnyServerCertificateReason = "Should not be used"
        };

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, options, null);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithOptions_DangerousBypassTrue_ShouldSetCallback()
    {
        // Arrange
        var handler = new HttpClientHandler();
        var options = new HttpClientSecurityOptions
        {
            DangerousAcceptAnyServerCertificate = true,
            DangerousAcceptAnyServerCertificateReason = "Testing only"
        };
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, options, logger);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithBoolOverload_AndTrue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, true, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HttpClientSecurityOptions*");
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithBoolOverload_AndFalse_ShouldNotThrow()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, false, null);

        // Assert
        result.Should().BeSameAs(handler);
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    #endregion

    #region Certificate Validation Tests

    [Fact]
    public void CreateCertificateValidationCallback_WithValidCertificate_ShouldReturnTrue()
    {
        // Arrange
        RemoteCertificateValidationCallback callback = HttpClientSecurityExtensions.CreateCertificateValidationCallback();

        // Act
        bool result = callback(null!, null, null, SslPolicyErrors.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CreateCertificateValidationCallback_WithCertificateError_ShouldReturnFalse()
    {
        // Arrange
        RemoteCertificateValidationCallback callback = HttpClientSecurityExtensions.CreateCertificateValidationCallback();

        // Act
        bool result = callback(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Dangerous Bypass Tests

    [Fact]
    public void CreateDangerousBypassCallback_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.CreateDangerousBypassCallback(null!, "test");
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void CreateDangerousBypassCallback_WithEmptyReason_ShouldThrowArgumentException()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "");
        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    [Fact]
    public void CreateDangerousBypassCallback_WithNullReason_ShouldThrowArgumentException()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, null!);
        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    [Fact]
    public void CreateDangerousBypassCallback_WithWhitespaceReason_ShouldThrowArgumentException()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "   ");
        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    [Fact]
    public void CreateDangerousBypassCallback_WithValidInputs_ShouldReturnTrueForAnyError()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();
        Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback = HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "Testing");

        // Act
        bool result = callback(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CreateDangerousBypassCallback_WithValidInputs_ShouldReturnTrueForNoError()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();
        Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback = HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "Testing");

        // Act
        bool result = callback(null!, null, null, SslPolicyErrors.None);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Client Certificate Tests

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithNullClientCertificate_ShouldNotSetClientCertificates()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, (X509Certificate2?)null);

        // Assert
        result.ClientCertificates.Count.Should().Be(0);
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithClientCertificate_ShouldAddToCollection()
    {
        // Arrange
        var handler = new HttpClientHandler();
        // Create a self-signed certificate for testing
        using X509Certificate2 certificate = CreateTestCertificate();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, certificate);

        // Assert
        result.ClientCertificateOptions.Should().Be(ClientCertificateOption.Manual);
        result.ClientCertificates.Count.Should().Be(1);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        // Create a test certificate using RSA
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var subjectName = new X500DistinguishedName("CN=TestCertificate");
        var request = new CertificateRequest(
            subjectName,
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        // Create a self-signed certificate with 1 day validity
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        // Export and re-import to create a standalone certificate using the modern API
        byte[] pfxData = certificate.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfxData, null, X509KeyStorageFlags.EphemeralKeySet);
    }

    #endregion

    #region Security Requirements Tests

    [Fact]
    public void ValidationCannotBeBypassedAccidentally_WhenUsingDefaults_ShouldNotAllowBypass()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    [Fact]
    public void ExplicitOptInIsRequired_DangerousBypassRequiresOptionsParameter()
    {
        // Arrange & Act & Assert
        Action act = () => HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(new HttpClientHandler(), true, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExplicitOptInIsRequired_DangerousBypassRequiresReason()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act & Assert
        Action act = () => HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Options_DangerousAcceptAnyServerCertificate_DefaultsToFalse()
    {
        // Arrange & Act
        var options = new HttpClientSecurityOptions();

        // Assert
        options.DangerousAcceptAnyServerCertificate.Should().BeFalse();
    }

    [Fact]
    public void Options_DangerousAcceptAnyServerCertificateReason_DefaultsToNull()
    {
        // Arrange & Act
        var options = new HttpClientSecurityOptions();

        // Assert
        options.DangerousAcceptAnyServerCertificateReason.Should().BeNull();
    }

    #endregion

    #region Audit Logging Tests

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)]
    public void CreateDangerousBypassCallback_WithAnySslError_ReturnsTrueAndBypasses(SslPolicyErrors sslPolicyErrors)
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();
        Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback = HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "Test bypass");

        // Act
        bool result = callback(null!, null, null, sslPolicyErrors);

        // Assert
        result.Should().BeTrue("dangerous bypass callback should always return true regardless of SSL errors");
    }

    [Fact]
    public void CreateDangerousBypassCallback_WhenCalledWithNullRequestMessage_ShouldNotThrow()
    {
        // Arrange
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();
        Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback = HttpClientSecurityExtensions.CreateDangerousBypassCallback(logger, "Testing with null request");

        // Act & Assert
        Action act = () => callback(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithDangerousBypass_RequiresExplicitReason()
    {
        // Arrange
        var handler = new HttpClientHandler();
        var options = new HttpClientSecurityOptions
        {
            DangerousAcceptAnyServerCertificate = true,
            DangerousAcceptAnyServerCertificateReason = "Integration testing with self-signed certificates"
        };
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, options, logger);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().NotBeNull();
        // The callback should be set and should return true for any error
        bool bypassResult = result.ServerCertificateCustomValidationCallback!(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        bypassResult.Should().BeTrue();
    }

    [Fact]
    public void ConfigureSecureHttpClientHandler_WithDangerousBypassAndNoReason_UsesDefaultReason()
    {
        // Arrange
        var handler = new HttpClientHandler();
        var options = new HttpClientSecurityOptions
        {
            DangerousAcceptAnyServerCertificate = true
            // DangerousAcceptAnyServerCertificateReason is null
        };
        ILogger logger = new LoggerFactory().CreateLogger<HttpClientSecurityExtensionsTests>();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, options, logger);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().NotBeNull();
        // Should still work even without explicit reason (falls back to default)
        bool bypassResult = result.ServerCertificateCustomValidationCallback!(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        bypassResult.Should().BeTrue();
    }

    #endregion

    #region Security Compliance Tests

    [Fact]
    public void DangerousBypassCannotBeEnabledWithoutOptionsParameter()
    {
        // This test verifies that the bool overload (the old API) cannot be used
        // to enable dangerous bypass - it must use the options-based overload

        // Arrange
        var handler = new HttpClientHandler();

        // Act & Assert - calling with true should throw
        Action act = () => HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, true, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefaultConfiguration_DoesNotAllowAnyCertificate()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
        // When callback is null, the system's default certificate validation is used
    }

    [Fact]
    public void NullOptions_DoesNotAllowCertificateBypass()
    {
        // Arrange
        var handler = new HttpClientHandler();

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, null, null);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    [Fact]
    public void OptionsWithExplicitlyDisabledBypass_DoesNotAllowCertificateBypass()
    {
        // Arrange
        var handler = new HttpClientHandler();
        var options = new HttpClientSecurityOptions
        {
            DangerousAcceptAnyServerCertificate = false,
            DangerousAcceptAnyServerCertificateReason = "Should not matter since bypass is disabled"
        };

        // Act
        HttpClientHandler result = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler, options, null);

        // Assert
        result.ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    #endregion
}
