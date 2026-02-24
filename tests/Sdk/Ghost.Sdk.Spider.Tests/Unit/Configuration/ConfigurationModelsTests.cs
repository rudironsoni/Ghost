using FluentAssertions;
using Ghost.Sdk.Spider.Configuration;
using Ghost.Sdk.Spider.Configuration.Models;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Configuration;

/// <summary>
/// Comprehensive tests for configuration model DTOs.
/// Tests property getters/setters and default values.
/// </summary>
public class ConfigurationModelsTests : ReliabilityTestBase
{
    public ConfigurationModelsTests(ITestOutputHelper output) : base(output) { }
    private static readonly string[] ExpectedScopes = new[] { "read", "write" };
    private static readonly string[] ExpectedChannels = new[] { "email", "slack" };
    private static readonly string[] ExpectedContentTypes = new[] { "image/*", "video/*", "audio/*", "font/*" };
    
    [Fact]
    public void SpiderConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new SpiderConfiguration();

        // Assert
        config.Id.Should().Be(string.Empty);
        config.Name.Should().Be(string.Empty);
        config.Version.Should().Be("1.0.0");
        config.Description.Should().BeNull();
        config.Tags.Should().NotBeNull().And.BeEmpty();
        config.Target.Should().NotBeNull();
        config.Extraction.Should().BeNull();
        config.Navigation.Should().NotBeNull();
        config.Strategies.Should().NotBeNull();
        config.Pipeline.Should().NotBeNull();
        config.Storage.Should().NotBeNull();
        config.Schedule.Should().BeNull();
        config.Monitoring.Should().NotBeNull();
        config.Limits.Should().NotBeNull();
        config.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SpiderConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new SpiderConfiguration();

        // Act
        config.Id = "test-id";
        config.Name = "Test Spider";
        config.Version = "2.0.0";
        config.Description = "Test description";
        config.Tags.Add("test-tag");

        // Assert
        config.Id.Should().Be("test-id");
        config.Name.Should().Be("Test Spider");
        config.Version.Should().Be("2.0.0");
        config.Description.Should().Be("Test description");
        config.Tags.Should().Contain("test-tag");
    }

    [Fact]
    public void TargetConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new TargetConfiguration();

        // Assert
        config.StartUrls.Should().NotBeNull().And.BeEmpty();
        config.AllowedPatterns.Should().NotBeNull().And.BeEmpty();
        config.DeniedPatterns.Should().NotBeNull().And.BeEmpty();
        config.AllowedDomains.Should().NotBeNull().And.BeEmpty();
        config.MaxDepth.Should().Be(0);
        config.FollowRedirects.Should().BeTrue();
        config.RespectRobotsTxt.Should().BeTrue();
        config.UserAgent.Should().Be("Ghost.Sdk.Spider/1.0");
        config.Headers.Should().NotBeNull().And.BeEmpty();
        config.Authentication.Should().BeNull();
    }

    [Fact]
    public void TargetConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new TargetConfiguration();

        // Act
        config.StartUrls.Add("https://example.com");
        config.MaxDepth = 5;
        config.FollowRedirects = false;
        config.UserAgent = "Custom Agent";
        config.Headers["X-Custom"] = "value";

        // Assert
        config.StartUrls.Should().Contain("https://example.com");
        config.MaxDepth.Should().Be(5);
        config.FollowRedirects.Should().BeFalse();
        config.UserAgent.Should().Be("Custom Agent");
        config.Headers["X-Custom"].Should().Be("value");
    }

    [Fact]
    public void AuthenticationConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new AuthenticationConfiguration();

        // Assert
        config.Type.Should().Be("Basic");
        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.Token.Should().BeNull();
        config.Cookies.Should().NotBeNull().And.BeEmpty();
        config.OAuth2.Should().BeNull();
    }

    [Fact]
    public void AuthenticationConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new AuthenticationConfiguration();

        // Act
        config.Type = "Bearer";
        config.Username = "testuser";
        config.Password = "testpass";
        config.Token = "token123";
        config.Cookies["sessionId"] = "abc123";

        // Assert
        config.Type.Should().Be("Bearer");
        config.Username.Should().Be("testuser");
        config.Password.Should().Be("testpass");
        config.Token.Should().Be("token123");
        config.Cookies["sessionId"].Should().Be("abc123");
    }

    [Fact]
    public void OAuth2Configuration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new OAuth2Configuration();

        // Assert
        config.TokenUrl.Should().Be(string.Empty);
        config.ClientId.Should().Be(string.Empty);
        config.ClientSecret.Should().Be(string.Empty);
        config.Scopes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void OAuth2Configuration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new OAuth2Configuration();

        // Act
        config.TokenUrl = "https://auth.example.com/token";
        config.ClientId = "client123";
        config.ClientSecret = "secret456";
        config.Scopes.Add("read");
        config.Scopes.Add("write");

        // Assert
        config.TokenUrl.Should().Be("https://auth.example.com/token");
        config.ClientId.Should().Be("client123");
        config.ClientSecret.Should().Be("secret456");
        config.Scopes.Should().Contain(ExpectedScopes);
    }

    [Fact]
    public void LimitsConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new LimitsConfiguration();

        // Assert
        config.MaxPages.Should().Be(0);
        config.MaxDurationSeconds.Should().Be(0);
        config.MaxFileSizeBytes.Should().Be(0);
        config.MaxTotalDownloadBytes.Should().Be(0);
        config.MaxMemoryBytes.Should().Be(0);
        config.MaxQueueSize.Should().Be(10000);
        config.MaxRetriesPerUrl.Should().Be(3);
        config.RequestTimeoutSeconds.Should().Be(30);
        config.PageLoadTimeoutSeconds.Should().Be(30);
        config.MaxBrowserContexts.Should().Be(5);
        config.AllowedContentTypes.Should().NotBeNull().And.BeEmpty();
        config.BlockedContentTypes.Should().Contain("image/*");
        config.ResourceBlocking.Should().NotBeNull();
    }

    [Fact]
    public void LimitsConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new LimitsConfiguration();

        // Act
        config.MaxPages = 1000;
        config.MaxDurationSeconds = 3600;
        config.MaxFileSizeBytes = 10485760;
        config.MaxQueueSize = 5000;
        config.MaxRetriesPerUrl = 5;
        config.RequestTimeoutSeconds = 60;

        // Assert
        config.MaxPages.Should().Be(1000);
        config.MaxDurationSeconds.Should().Be(3600);
        config.MaxFileSizeBytes.Should().Be(10485760);
        config.MaxQueueSize.Should().Be(5000);
        config.MaxRetriesPerUrl.Should().Be(5);
        config.RequestTimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void ResourceBlockingConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new ResourceBlockingConfiguration();

        // Assert
        config.BlockImages.Should().BeTrue();
        config.BlockStylesheets.Should().BeFalse();
        config.BlockFonts.Should().BeTrue();
        config.BlockMedia.Should().BeTrue();
        config.BlockedUrlPatterns.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ResourceBlockingConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new ResourceBlockingConfiguration();

        // Act
        config.BlockImages = false;
        config.BlockStylesheets = true;
        config.BlockFonts = false;
        config.BlockMedia = false;
        config.BlockedUrlPatterns.Add("*/ads/*");

        // Assert
        config.BlockImages.Should().BeFalse();
        config.BlockStylesheets.Should().BeTrue();
        config.BlockFonts.Should().BeFalse();
        config.BlockMedia.Should().BeFalse();
        config.BlockedUrlPatterns.Should().Contain("*/ads/*");
    }

    [Fact]
    public void MonitoringConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new MonitoringConfiguration();

        // Assert
        config.Enabled.Should().BeTrue();
        config.CollectMetrics.Should().BeTrue();
        config.EmitDiagnostics.Should().BeTrue();
        config.MetricsExportIntervalSeconds.Should().Be(60);
        config.Logging.Should().NotBeNull();
        config.Telemetry.Should().NotBeNull();
        config.HealthCheck.Should().NotBeNull();
        config.Alerts.Should().NotBeNull();
    }

    [Fact]
    public void MonitoringConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new MonitoringConfiguration();

        // Act
        config.Enabled = false;
        config.CollectMetrics = false;
        config.EmitDiagnostics = false;
        config.MetricsExportIntervalSeconds = 120;

        // Assert
        config.Enabled.Should().BeFalse();
        config.CollectMetrics.Should().BeFalse();
        config.EmitDiagnostics.Should().BeFalse();
        config.MetricsExportIntervalSeconds.Should().Be(120);
    }

    [Fact]
    public void LoggingConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new LoggingConfiguration();

        // Assert
        config.MinimumLevel.Should().Be("Information");
        config.LogSuccessfulExtractions.Should().BeFalse();
        config.LogFailedExtractions.Should().BeTrue();
        config.IncludeExtractedData.Should().BeFalse();
        config.Enrichers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void LoggingConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new LoggingConfiguration();

        // Act
        config.MinimumLevel = "Debug";
        config.LogSuccessfulExtractions = true;
        config.LogFailedExtractions = false;
        config.IncludeExtractedData = true;
        config.Enrichers.Add("CustomEnricher");

        // Assert
        config.MinimumLevel.Should().Be("Debug");
        config.LogSuccessfulExtractions.Should().BeTrue();
        config.LogFailedExtractions.Should().BeFalse();
        config.IncludeExtractedData.Should().BeTrue();
        config.Enrichers.Should().Contain("CustomEnricher");
    }

    [Fact]
    public void TelemetryConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new TelemetryConfiguration();

        // Assert
        config.ExportTraces.Should().BeFalse();
        config.ExportMetrics.Should().BeFalse();
        config.OtlpEndpoint.Should().BeNull();
        config.CustomAttributes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TelemetryConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new TelemetryConfiguration();

        // Act
        config.ExportTraces = true;
        config.ExportMetrics = true;
        config.OtlpEndpoint = "http://localhost:4317";
        config.CustomAttributes["service.name"] = "spider";

        // Assert
        config.ExportTraces.Should().BeTrue();
        config.ExportMetrics.Should().BeTrue();
        config.OtlpEndpoint.Should().Be("http://localhost:4317");
        config.CustomAttributes["service.name"].Should().Be("spider");
    }

    [Fact]
    public void HealthCheckConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new HealthCheckConfiguration();

        // Assert
        config.Enabled.Should().BeTrue();
        config.IntervalSeconds.Should().Be(30);
        config.TimeoutSeconds.Should().Be(10);
        config.CustomChecks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void HealthCheckConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new HealthCheckConfiguration();

        // Act
        config.Enabled = false;
        config.IntervalSeconds = 60;
        config.TimeoutSeconds = 20;
        config.CustomChecks.Add("DatabaseCheck");

        // Assert
        config.Enabled.Should().BeFalse();
        config.IntervalSeconds.Should().Be(60);
        config.TimeoutSeconds.Should().Be(20);
        config.CustomChecks.Should().Contain("DatabaseCheck");
    }

    [Fact]
    public void AlertConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new AlertConfiguration();

        // Assert
        config.Enabled.Should().BeFalse();
        config.Rules.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void AlertConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new AlertConfiguration();
        var rule = new AlertRuleConfiguration
        {
            Name = "HighErrorRate",
            Condition = "error_rate > 0.1"
        };

        // Act
        config.Enabled = true;
        config.Rules.Add(rule);

        // Assert
        config.Enabled.Should().BeTrue();
        config.Rules.Should().HaveCount(1);
        config.Rules[0].Name.Should().Be("HighErrorRate");
    }

    [Fact]
    public void AlertRuleConfiguration_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new AlertRuleConfiguration();

        // Assert
        config.Name.Should().Be(string.Empty);
        config.Condition.Should().Be(string.Empty);
        config.Severity.Should().Be("Warning");
        config.Channels.Should().NotBeNull().And.BeEmpty();
        config.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void AlertRuleConfiguration_Properties_ShouldGetAndSet()
    {
        // Arrange
        var config = new AlertRuleConfiguration();

        // Act
        config.Name = "TestRule";
        config.Condition = "test > 0";
        config.Severity = "Critical";
        config.Channels.Add("email");
        config.Channels.Add("slack");
        config.Metadata["owner"] = "team-a";

        // Assert
        config.Name.Should().Be("TestRule");
        config.Condition.Should().Be("test > 0");
        config.Severity.Should().Be("Critical");
        config.Channels.Should().Contain(ExpectedChannels);
        config.Metadata["owner"].Should().Be("team-a");
    }

    [Fact]
    public void NavigationConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var navConfig = new NavigationConfiguration();

        // Act
        spiderConfig.Navigation = navConfig;

        // Assert
        spiderConfig.Navigation.Should().BeSameAs(navConfig);
    }

    [Fact]
    public void StrategiesConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var stratConfig = new StrategiesConfiguration();

        // Act
        spiderConfig.Strategies = stratConfig;

        // Assert
        spiderConfig.Strategies.Should().BeSameAs(stratConfig);
    }

    [Fact]
    public void PipelineConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var pipelineConfig = new PipelineConfiguration();

        // Act
        spiderConfig.Pipeline = pipelineConfig;

        // Assert
        spiderConfig.Pipeline.Should().BeSameAs(pipelineConfig);
    }

    [Fact]
    public void StorageConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var storageConfig = new StorageConfiguration();

        // Act
        spiderConfig.Storage = storageConfig;

        // Assert
        spiderConfig.Storage.Should().BeSameAs(storageConfig);
    }

    [Fact]
    public void ScheduleConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var scheduleConfig = new ScheduleConfiguration();

        // Act
        spiderConfig.Schedule = scheduleConfig;

        // Assert
        spiderConfig.Schedule.Should().BeSameAs(scheduleConfig);
    }

    [Fact]
    public void ExtractionConfiguration_CanBeAssigned()
    {
        // Arrange
        var spiderConfig = new SpiderConfiguration();
        var extractionConfig = new ExtractionConfiguration();

        // Act
        spiderConfig.Extraction = extractionConfig;

        // Assert
        spiderConfig.Extraction.Should().BeSameAs(extractionConfig);
    }

    [Fact]
    public void SpiderConfiguration_Metadata_ShouldSupportMultipleTypes()
    {
        // Arrange
        var config = new SpiderConfiguration();

        // Act
        config.Metadata["stringValue"] = "test";
        config.Metadata["intValue"] = 123;
        config.Metadata["boolValue"] = true;
        config.Metadata["dictValue"] = new Dictionary<string, string> { ["key"] = "value" };

        // Assert
        config.Metadata.Should().HaveCount(4);
        config.Metadata["stringValue"].Should().Be("test");
        config.Metadata["intValue"].Should().Be(123);
        config.Metadata["boolValue"].Should().Be(true);
        config.Metadata["dictValue"].Should().BeOfType<Dictionary<string, string>>();
    }

    [Fact]
    public void LimitsConfiguration_BlockedContentTypes_ShouldContainDefaultValues()
    {
        // Act
        var config = new LimitsConfiguration();

        // Assert
        config.BlockedContentTypes.Should().Contain(ExpectedContentTypes);
    }

    [Fact]
    public void AuthenticationConfiguration_WithOAuth2_ShouldWork()
    {
        // Arrange
        var config = new AuthenticationConfiguration
        {
            Type = "OAuth2",
            OAuth2 = new OAuth2Configuration
            {
                TokenUrl = "https://auth.example.com/token",
                ClientId = "client123",
                ClientSecret = "secret456",
                Scopes = new List<string> { "read", "write" }
            }
        };

        // Assert
        config.Type.Should().Be("OAuth2");
        config.OAuth2.Should().NotBeNull();
        config.OAuth2!.TokenUrl.Should().Be("https://auth.example.com/token");
        config.OAuth2.ClientId.Should().Be("client123");
        config.OAuth2.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void TargetConfiguration_WithAuthentication_ShouldWork()
    {
        // Arrange
        var config = new TargetConfiguration
        {
            Authentication = new AuthenticationConfiguration
            {
                Type = "Basic",
                Username = "user",
                Password = "pass"
            }
        };

        // Assert
        config.Authentication.Should().NotBeNull();
        config.Authentication!.Type.Should().Be("Basic");
        config.Authentication.Username.Should().Be("user");
        config.Authentication.Password.Should().Be("pass");
    }

    [Fact]
    public void MonitoringConfiguration_WithAllSubconfigurations_ShouldWork()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            Enabled = true,
            Logging = new LoggingConfiguration { MinimumLevel = "Debug" },
            Telemetry = new TelemetryConfiguration { ExportTraces = true },
            HealthCheck = new HealthCheckConfiguration { IntervalSeconds = 60 },
            Alerts = new AlertConfiguration { Enabled = true }
        };

        // Assert
        config.Enabled.Should().BeTrue();
        config.Logging.MinimumLevel.Should().Be("Debug");
        config.Telemetry.ExportTraces.Should().BeTrue();
        config.HealthCheck.IntervalSeconds.Should().Be(60);
        config.Alerts.Enabled.Should().BeTrue();
    }
}
