using System.Net;
using Ghost.Cloud.Api.Canaries;
using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit.Abstractions;

namespace Ghost.Cloud.Api.UnitTests.Canaries;

public sealed class AssuranceCanaryRunnerTests : ReliabilityTestBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGhostEngine _engine;
    private readonly IDownloader _downloader;
    private readonly IEndpointGrain _endpointGrain;
    private readonly ITenantGrain _tenantGrain;

    public AssuranceCanaryRunnerTests(ITestOutputHelper output) : base(output)
    {
        _clusterClient = Substitute.For<IClusterClient>();
        _engine = Substitute.For<IGhostEngine>();
        _downloader = Substitute.For<IDownloader>();
        _endpointGrain = Substitute.For<IEndpointGrain>();
        _tenantGrain = Substitute.For<ITenantGrain>();

        _clusterClient.GetGrain<IEndpointGrain>(Arg.Any<string>()).Returns(_endpointGrain);
        _clusterClient.GetGrain<ITenantGrain>(Arg.Any<Guid>()).Returns(_tenantGrain);
    }

    private AssuranceCanaryRunner CreateRunner()
    {
        return new AssuranceCanaryRunner(
            _clusterClient,
            _engine,
            _downloader,
            NullLogger<AssuranceCanaryRunner>.Instance);
    }

    private static ScheduledRunInfo CreateScheduledRun(
        string runId = "test-run-1",
        string endpointId = "test-endpoint",
        string runKind = "canary")
    {
        return new ScheduledRunInfo
        {
            RunId = runId,
            EndpointId = endpointId,
            TenantId = Guid.NewGuid(),
            ScheduledTime = DateTimeOffset.UtcNow.AddSeconds(-10),
            Status = "Dispatching",
            RunKind = runKind,
            RequestedMode = "canary",
            Input = JsonDocument.Parse("{\"url\":\"https://example.com\"}").RootElement
        };
    }

    private static EndpointManifest CreateEndpointManifest(
        EndpointCapability capability = EndpointCapability.Discovery)
    {
        return new EndpointManifest
        {
            EndpointId = "test-endpoint",
            PluginId = "test-plugin",
            Version = "1.0.0",
            DisplayName = "Test Endpoint",
            Capability = capability,
            SupportedDeliveryModes = ["sync", "async"],
            SupportsArtifacts = true
        };
    }

    [Fact]
    public async Task RunAsync_ReturnsSuccess_WhenCanarySucceedsAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Classification.Should().Be("Success");
        outcome.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithConfigurationError_WhenInputValidationFailsAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>())
            .ThrowsAsync(new ArgumentException("Invalid input schema"));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("ConfigurationError");
        outcome.ErrorMessage.Should().Contain("Input validation failed");
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithTimeout_WhenOperationIsCancelledAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.Delay(TimeSpan.FromSeconds(60), c.Arg<CancellationToken>()));

        AssuranceCanaryRunner runner = CreateRunner();
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, cts.Token);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("Cancelled");
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithRateLimited_WhenHttp429ReceivedAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("RateLimited");
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithEndpointError_WhenHttp5xxReceivedAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("EndpointError");
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithNetworkError_WhenHttp4xxReceivedAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("NetworkError");
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WithUnknown_WhenUnexpectedExceptionOccursAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Classification.Should().Be("Unknown");
        outcome.ErrorMessage.Should().Contain("Unexpected error");
    }

    [Fact]
    public async Task RunAsync_CallsEndpointGrain_ToGetManifestAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun(endpointId: "my-endpoint");
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        await _endpointGrain.Received(1).GetManifestAsync();
        _clusterClient.Received(1).GetGrain<IEndpointGrain>("my-endpoint");
    }

    [Fact]
    public async Task RunAsync_CallsValidateInput_WithCorrectInputAsync()
    {
        // Arrange
        JsonElement input = JsonDocument.Parse("{\"test\":\"value\"}").RootElement;
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        scheduledRun = scheduledRun with { Input = input };
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        await _endpointGrain.Received(1).ValidateInputAsync(Arg.Is<JsonElement>(e => e.GetProperty("test").GetString() == "value"));
    }

    [Fact]
    public async Task RunAsync_CallsEngineRun_WithCorrectContextAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun(runId: "run-123", endpointId: "endpoint-456");
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);

        GhostEngineContext? capturedContext = null;

        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                capturedContext = c.Arg<GhostEngineContext>();
                return Task.CompletedTask;
            });

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.JobId.Should().Be("run-123");
        capturedContext.Metadata["endpointId"].Should().Be("endpoint-456");
        capturedContext.Metadata["tenantId"].Should().Be(scheduledRun.TenantId);
    }

    [Fact]
    public async Task RunAsync_ReturnsOutcome_WithDiagnosticsUri_WhenFailedAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Test error"));

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.DiagnosticsUri.Should().NotBeNullOrEmpty();
        outcome.DiagnosticsUri.Should().StartWith("ghost://diagnostics/canary/");
    }

    [Fact]
    public async Task RunAsync_ReturnsOutcome_WithoutDiagnosticsUri_WhenSuccessfulAsync()
    {
        // Arrange
        ScheduledRunInfo scheduledRun = CreateScheduledRun();
        EndpointManifest manifest = CreateEndpointManifest();

        _endpointGrain.GetManifestAsync().Returns(manifest);
        _endpointGrain.ValidateInputAsync(Arg.Any<JsonElement>()).Returns(Task.CompletedTask);
        _engine.RunAsync(Arg.Any<ISpider>(), Arg.Any<GhostEngineContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        AssuranceCanaryRunner runner = CreateRunner();

        // Act
        CanaryRunOutcome outcome = await runner.RunAsync(scheduledRun, CancellationToken.None);

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.DiagnosticsUri.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ThrowsArgumentNullException_WhenScheduledRunIsNullAsync()
    {
        // Arrange
        AssuranceCanaryRunner runner = CreateRunner();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await runner.RunAsync(null!, CancellationToken.None));
    }
}
