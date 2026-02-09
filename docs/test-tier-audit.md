# Test Tier Audit Report

This report classifies all test files in the Ghost project as Unit, Integration, or E2E.

**Classification Rules:**
- **Unit**: Pure in-memory, mocks only, no IO, no browsers, <100ms execution
- **Integration**: Uses real GhostKernel/Playwright, databases, network calls
- **E2E**: Full end-to-end scenarios, multiple components, browser automation

**Audit Methodology:**
1. Scan all .cs files in tests/ directory (excluding obj/bin)
2. Analyze each file for dependencies (GhostKernel, Playwright, mocks)
3. Classify based on content and path patterns
4. Count test methods using [Fact] and [Theory] patterns
5. Exclude files with zero test methods (non-test files)

Generated: Mon Feb  9 05:49:43 PM CET 2026

## Summary
- **Total test files**: 193
- **Unit tests**: 174
- **Integration tests**: 17
- **E2E tests**: 2

## Unit Tests (174 files)

- `tests/Core/Ghost.Tests/Core/SessionOptionsTests.cs`
  - Classes: SessionOptionsTests
  - Tests: ~2
  - Dependencies: SessionOptions

- `tests/Core/Ghost.Tests/Core/GhostKernelTests.cs`
  - Classes: GhostKernelTests
  - Tests: ~4
  - Dependencies: Microsoft.Playwright, CancellationTokenSource, System.Threading, System.Reflection, System.Threading.Tasks

- `tests/Core/Ghost.Tests/Core/KernelOptionsTests.cs`
  - Classes: KernelOptionsTests
  - Tests: ~2
  - Dependencies: KernelOptions

- `tests/Core/Ghost.Tests/Consent/ShadowDOMHelperTests.cs`
  - Classes: ShadowDOMHelperTests
  - Tests: ~8
  - Dependencies: Microsoft.Playwright, Ghost.Consent, InvalidOperationException

- `tests/Core/Ghost.Tests/Consent/CMPDatabaseTests.cs`
  - Classes: CMPDatabaseTests
  - Tests: ~12
  - Dependencies: Ghost.Consent

- `tests/Core/Ghost.Tests/Consent/CMPConfigTests.cs`
  - Classes: CMPConfigTests
  - Tests: ~7
  - Dependencies: Ghost.Consent

- `tests/Core/Ghost.Tests/Consent/ConsentHandlerTests.cs`
  - Classes: ConsentHandlerTests
  - Tests: ~10
  - Dependencies: ConsentHandler, Microsoft.Playwright, Microsoft.Extensions.Logging.Abstractions, Ghost.Consent

- `tests/Core/Ghost.Tests/Consent/RegionDetectorTests.cs`
  - Classes: RegionDetectorTests
  - Tests: ~10
  - Dependencies: Microsoft.Playwright, Ghost.Consent, InvalidOperationException

- `tests/Core/Ghost.Tests/Consent/ConsentFlowHandlerTests.cs`
  - Classes: ConsentFlowHandlerTests
  - Tests: ~9
  - Dependencies: Microsoft.Playwright, Ghost.Consent, ConsentFlowHandler

- `tests/Core/Ghost.Tests/Extensions/ServiceCollectionExtensionsTests.cs`
  - Classes: ServiceCollectionExtensionsTests
  - Tests: ~1
  - Dependencies: Microsoft.Extensions.DependencyInjection, ServiceCollection, Ghost, Microsoft

- `tests/Core/Ghost.Tests/Extensions/HumanInteractionExtensionsTests.cs`
  - Classes: HumanInteractionExtensionsTests
  - Tests: ~2
  - Dependencies: Ghost.Extensions, System.Threading.Tasks, System.Threading

- `tests/Core/Ghost.Tests/Services/StaticProxySourceTests.cs`
  - Classes: StaticProxySourceTests
  - Tests: ~4
  - Dependencies: StaticProxySource, System.Linq, Microsoft.Extensions.Logging.Abstractions, System.Threading, System.Threading.Tasks

- `tests/Core/Ghost.Tests/Services/AggregatedJobClientIntegrationTests.cs`
  - Classes: AggregatedJobClientIntegrationTests
  - Tests: ~21
  - Dependencies: System.Linq, HttpRequestException, OperationCanceledException, CancellationTokenSource, System.Threading

- `tests/Core/Ghost.Tests/ProxyManagement/FreeProxyScraperTests.cs`
  - Classes: FreeProxyScraperTests
  - Tests: ~3
  - Dependencies: System.Linq, System.Net.Http, Microsoft.Extensions.Logging.Abstractions, StringContent, System.Threading

- `tests/Core/Ghost.Tests/ProxyManagement/FreeProxyHealthCheckerTests.cs`
  - Classes: FreeProxyHealthCheckerTests
  - Tests: ~6
  - Dependencies: Microsoft.Extensions.Logging.Abstractions, System.Threading, System.Threading.Tasks, Ghost.ProxyManagement, System.Net

- `tests/Core/Ghost.Tests/ProxyManagement/ProxyGeographicFilterTests.cs`
  - Classes: ProxyGeographicFilterTests
  - Tests: ~4
  - Dependencies: Microsoft.Extensions.Logging.Abstractions, System.Threading, System.Threading.Tasks, Ghost.ProxyManagement, ProxyGeographicFilter

- `tests/Core/Ghost.Tests/ProxyManagement/RotatingProxyPoolTests.cs`
  - Classes: RotatingProxyPoolTests
  - Tests: ~4
  - Dependencies: Microsoft.Extensions.Logging.Abstractions, CancellationTokenSource, System.Threading, System.Threading.Tasks, RotatingProxyPool

- `tests/Core/Ghost.Tests/Resilience/RetryPolicyOptionsTests.cs`
  - Classes: RetryPolicyOptionsTests
  - Tests: ~1
  - Dependencies: Ghost.Resilience, RetryPolicyOptions

- `tests/Core/Ghost.Tests/Resilience/RetryableErrorClassifierTests.cs`
  - Classes: RetryableErrorClassifierTests
  - Tests: ~8
  - Dependencies: NotSupportedException, System.ComponentModel.DataAnnotations, HttpRequestException, System.Net.Http, System.Net

- `tests/Core/Ghost.Tests/Resilience/FileSystemDeadLetterQueueTests.cs`
  - Classes: FileSystemDeadLetterQueueTests
  - Tests: ~12
  - Dependencies: System.Linq, Microsoft.Extensions.Logging.Abstractions, System.IO, System.Threading.Tasks, FileSystemDeadLetterQueue

- `tests/Core/Ghost.Tests/Resilience/RetryPolicyTests.cs`
  - Classes: RetryPolicyTests
  - Tests: ~17
  - Dependencies: RetryPolicy, HttpRequestException, System.Net.Http, System.Diagnostics, InvalidOperationException

- `tests/Core/Ghost.Tests/Session/SessionManagerTests.cs`
  - Classes: SessionManagerTests
  - Tests: ~4
  - Dependencies: Microsoft.Playwright, Microsoft.Extensions.Options, Ghost.Session, SessionManager

- `tests/Core/Ghost.Tests/Stealth/FingerprintProfileTests.cs`
  - Classes: FingerprintProfileTests
  - Tests: ~2

- `tests/Core/Ghost.Tests/Stealth/FingerprintGeneratorTests.cs`
  - Classes: FingerprintGeneratorTests
  - Tests: ~2
  - Dependencies: Ghost.Stealth

- `tests/Core/Ghost.Tests/Stealth/StealthScriptsTests.cs`
  - Classes: StealthScriptsTests
  - Tests: ~2
  - Dependencies: Ghost.Stealth

- `tests/Core/Ghost.Tests/Stealth/TLS/BrowserProfilesTests.cs`
  - Classes: BrowserProfilesTests
  - Tests: ~10
  - Dependencies: Ghost.Stealth.TLS, Random

- `tests/Core/Ghost.Tests/Stealth/TLS/JA3RandomizerTests.cs`
  - Classes: JA3RandomizerTests
  - Tests: ~13
  - Dependencies: Ghost.Stealth.TLS, JA3Randomizer

- `tests/Core/Ghost.Tests/Stealth/TLS/JA3ProfileTests.cs`
  - Classes: JA3ProfileTests
  - Tests: ~6
  - Dependencies: Ghost.Stealth.TLS

- `tests/Core/Ghost.Tests/Stealth/TLS/TLSFingerprintServiceTests.cs`
  - Classes: TLSFingerprintServiceTests
  - Tests: ~10
  - Dependencies: Ghost.Stealth.TLS, TLSFingerprintService, Microsoft.Extensions.Logging

- `tests/Core/Ghost.Tests/Stealth/Behavior/MouseMimicryTests.cs`
  - Classes: MouseMimicryTests
  - Tests: ~3
  - Dependencies: Ghost.Stealth.Behavior, MouseMimicry, Microsoft.Playwright

- `tests/Core/Ghost.Tests/Stealth/Behavior/TimingMimicryTests.cs`
  - Classes: TimingMimicryTests
  - Tests: ~6
  - Dependencies: Ghost.Stealth.Behavior, TimingMimicry

- `tests/Core/Ghost.Tests/Abstractions/ClickOptionsTests.cs`
  - Classes: ClickOptionsTests
  - Tests: ~2
  - Dependencies: ClickOptions, System.Linq

- `tests/Core/Ghost.Tests/Abstractions/NavigationOptionsTests.cs`
  - Classes: NavigationOptionsTests
  - Tests: ~2
  - Dependencies: NavigationOptions

- `tests/Core/Ghost.Tests/Abstractions/TypeOptionsTests.cs`
  - Classes: TypeOptionsTests
  - Tests: ~2
  - Dependencies: TypeOptions

- `tests/Core/Ghost.Tests/Abstractions/WaitOptionsTests.cs`
  - Classes: WaitOptionsTests
  - Tests: ~2
  - Dependencies: WaitOptions

- `tests/Core/Ghost.Tests/Abstractions/PageOptionsTests.cs`
  - Classes: PageOptionsTests
  - Tests: ~2
  - Dependencies: PageOptions

- `tests/Core/Ghost.Tests/Abstractions/ScreenshotOptionsTests.cs`
  - Classes: ScreenshotOptionsTests
  - Tests: ~2
  - Dependencies: ScreenshotOptions

- `tests/Core/Ghost.Tests/Monitoring/MetricsServiceTests.cs`
  - Classes: MetricsServiceTests
  - Tests: ~3
  - Dependencies: Ghost.Monitoring, MetricsService

- `tests/Core/Ghost.Tests/Monitoring/HealthReportServiceTests.cs`
  - Classes: HealthReportServiceTests
  - Tests: ~4
  - Dependencies: CancellationTokenSource, HealthReportService, Ghost.Monitoring, Ghost.Abstractions, TestProxySource

- `tests/Platforms/Ghost.Platform.InfoJobs.Tests/InfoJobsExtensionTests.cs`
  - Classes: InfoJobsExtensionTests
  - Tests: ~1
  - Dependencies: Microsoft.Extensions.DependencyInjection, ConfigurationBuilder, ServiceCollection, Microsoft.Extensions.Configuration

- `tests/Platforms/Ghost.Platform.InfoJobs.Tests/SalaryParsingTests.cs`
  - Classes: SalaryParsingTests
  - Tests: ~6
  - Dependencies: Ghost.Contracts.Jobs, Ghost.Platform.InfoJobs.Jobs.Internal

- `tests/Platforms/Ghost.Platform.Google.Tests/GoogleClientTests.cs`
  - Classes: GoogleClientTests
  - Tests: ~2
  - Dependencies: GeminiClient, Microsoft.Playwright, Ghost.Platform.Google.Gemini, System.Threading, Microsoft.Extensions.Logging

- `tests/Platforms/Ghost.Platform.Google.Tests/GoogleJobsApiClientIntegrationTests.cs`
  - Classes: GoogleJobsApiClientIntegrationTests
  - Tests: ~11
  - Dependencies: GoogleJobsApiClient, HttpRequestException, System.Net.Http, System.Text, System.Net

- `tests/Platforms/Ghost.Platform.Google.Tests/GoogleOptionsTests.cs`
  - Classes: GoogleOptionsTests
  - Tests: ~2
  - Dependencies: GoogleOptions

- `tests/Platforms/Ghost.Platform.Google.Tests/GoogleJobsParserIntegrationTests.cs`
  - Classes: GoogleJobsParserIntegrationTests
  - Tests: ~16
  - Dependencies: Microsoft.Extensions.Logging, Ghost.Contracts.Jobs, Ghost.Platform.Google.Jobs.Internal

- `tests/Platforms/Ghost.Platform.Google.Tests/GoogleExtensionTests.cs`
  - Classes: GoogleExtensionTests
  - Tests: ~2
  - Dependencies: ServiceCollection, Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, ConfigurationBuilder, GoogleExtension

- `tests/Platforms/Ghost.Platform.Anthropic.Tests/AnthropicOptionsTests.cs`
  - Classes: AnthropicOptionsTests
  - Tests: ~2
  - Dependencies: AnthropicOptions

- `tests/Platforms/Ghost.Platform.Anthropic.Tests/AnthropicClientTests.cs`
  - Classes: AnthropicClientTests
  - Tests: ~2
  - Dependencies: Microsoft.Playwright, System.Threading, Microsoft.Extensions.Logging, System.Threading.Tasks, AnthropicOptions

- `tests/Platforms/Ghost.Platform.Anthropic.Tests/AnthropicExtensionTests.cs`
  - Classes: AnthropicExtensionTests
  - Tests: ~4
  - Dependencies: AnthropicExtension

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInExtensionTests.cs`
  - Classes: LinkedInExtensionTests
  - Tests: ~1
  - Dependencies: ServiceCollection, INewsClient, LinkedInExtension, IJobClient, Microsoft.Extensions.DependencyInjection

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/GuestJobSearchParsingTests.cs`
  - Classes: GuestJobSearchParsingTests
  - Tests: ~2
  - Dependencies: System.Threading, System.Threading.Tasks, Ghost.Platform.LinkedIn.Internal, GuestJobSearch, Ghost.Contracts.Jobs

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInSessionPoolMetricsTests.cs`
  - Classes: LinkedInSessionPoolMetricsTests
  - Tests: ~1

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInSocialClientTests.cs`
  - Classes: LinkedInSocialClientTests
  - Tests: ~2
  - Dependencies: LinkedInSocialClient, LinkedInOptions, System.Linq, System.Threading, Microsoft.Extensions.Logging

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInJobClientParallelTests.cs`
  - Classes: LinkedInJobClientParallelTests
  - Tests: ~1
  - Dependencies: JavaScriptAdapter, object, Microsoft.Extensions.Logging.Abstractions, System.Threading, System.Threading.Tasks

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInNewsClientTests.cs`
  - Classes: LinkedInNewsClientTests
  - Tests: ~2
  - Dependencies: LinkedInOptions, System.Threading, LinkedInNewsClient, Microsoft.Extensions.Logging, Microsoft.Extensions.Options

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInJobClientTests.cs`
  - Classes: LinkedInJobClientTests
  - Tests: ~4
  - Dependencies: JavaScriptAdapter, System.Runtime.Serialization, System.Threading, Microsoft.Extensions.Logging, System.Threading.Tasks

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/BooleanExpressionTests.cs`
  - Classes: BooleanExpressionTests
  - Tests: ~5
  - Dependencies: Ghost.Platform.LinkedIn.Internal

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInOptionsTests.cs`
  - Classes: LinkedInOptionsTests
  - Tests: ~1
  - Dependencies: Microsoft.Extensions.DependencyInjection, LinkedInOptions

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInSessionPoolOptionsTests.cs`
  - Classes: LinkedInSessionPoolOptionsTests
  - Tests: ~1
  - Dependencies: LinkedInSessionPoolOptions

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/Internal/LinkedInSessionPoolTests.cs`
  - Classes: LinkedInSessionPoolTests
  - Tests: ~10
  - Dependencies: ValueTask, Microsoft.Extensions.Logging.Abstractions, System.Threading, System.Threading.Tasks, Ghost.Platform.LinkedIn.Internal

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/Internal/ParsingTests.cs`
  - Classes: ParsingTests
  - Tests: ~3

- `tests/Platforms/Ghost.Platform.OpenAI.Tests/OpenAIOptionsTests.cs`
  - Classes: OpenAIOptionsTests
  - Tests: ~2
  - Dependencies: OpenAIOptions

- `tests/Platforms/Ghost.Platform.OpenAI.Tests/OpenAIClientTests.cs`
  - Classes: OpenAIClientTests
  - Tests: ~2
  - Dependencies: OpenAIClient, Microsoft.Playwright, System.Threading, Ghost.Contracts.Inference, Microsoft.Extensions.Logging

- `tests/Platforms/Ghost.Platform.OpenAI.Tests/OpenAIExtensionTests.cs`
  - Classes: OpenAIExtensionTests
  - Tests: ~2
  - Dependencies: OpenAIExtension, Microsoft.Extensions.DependencyInjection, ServiceCollection, Microsoft.Extensions.Configuration

- `tests/Platforms/Ghost.Platform.Common.Tests/Session/SessionFactoryTests.cs`
  - Classes: SessionFactoryTests
  - Tests: ~5
  - Dependencies: Ghost.Platform.Common.Session, Ghost.Abstractions, RotatingProxySessionOptions, SessionFactory

- `tests/Platforms/Ghost.Platform.Common.Tests/Session/RotatingProxySessionTests.cs`
  - Classes: RotatingProxySessionTests
  - Tests: ~8
  - Dependencies: RotatingProxySession, System.Net.Http, HttpResponseMessage, System.Threading, System.Threading.Tasks

- `tests/Platforms/Ghost.Platform.Common.Tests/Session/SessionOrchestratorTests.cs`
  - Classes: SessionOrchestratorTests
  - Tests: ~8
  - Dependencies: Ghost.Pool, SessionOrchestrator, Microsoft.Extensions.Logging.Abstractions, SessionAffinityOptions, SessionAllocationContext

- `tests/Platforms/Ghost.Platform.X.Tests/XOptionsTests.cs`
  - Classes: XOptionsTests
  - Tests: ~11
  - Dependencies: XOptions, Microsoft.Extensions.Configuration

- `tests/Platforms/Ghost.Platform.X.Tests/XExtensionTests.cs`
  - Classes: XExtensionTests
  - Tests: ~14
  - Dependencies: IXMetricsService, Ghost.Platform.X.Services, Version, Microsoft.Extensions.DependencyInjection, ConfigurationBuilder

- `tests/Platforms/Ghost.Platform.X.Tests/XPostContentSplitterTests.cs`
  - Classes: XPostContentSplitterTests
  - Tests: ~22
  - Dependencies: XPostContentSplitter, Ghost.Platform.X.Internal, string

- `tests/Platforms/Ghost.Platform.X.Tests/XSimulationValidatorTests.cs`
  - Classes: XSimulationValidatorTests
  - Tests: ~25
  - Dependencies: XOptions, XSimulationValidator, Ghost.Platform.X.Internal, object, Ghost.Contracts.Simulation

- `tests/Platforms/Ghost.Platform.X.Tests/XSocialClientTests.cs`
  - Classes: XSocialClientTests
  - Tests: ~12
  - Dependencies: XOptions, Ghost.Platform.X.Internal, Microsoft.Extensions.Logging, XSocialClient, Ghost.Contracts.Social

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorJobParserTests.cs`
  - Classes: GlassdoorJobParserTests
  - Tests: ~9
  - Dependencies: Ghost.Platform.Glassdoor.Internal

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorSearchScraperTests.cs`
  - Classes: GlassdoorSearchScraperTests
  - Tests: ~6
  - Dependencies: GlassdoorSearchScraper, Ghost.Platform.Glassdoor.Jobs, Microsoft.Extensions.Logging, Microsoft.Extensions.Options, Ghost.Contracts.Jobs

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorApiClientIntegrationTests.cs`
  - Classes: GlassdoorApiClientIntegrationTests
  - Tests: ~15
  - Dependencies: HttpRequestException, GlassdoorApiClient, System.Net.Http, System.Text, System.Net

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorOptionsTests.cs`
  - Classes: GlassdoorOptionsTests
  - Tests: ~29
  - Dependencies: Ghost.Models, GlassdoorOptions

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorExtensionTests.cs`
  - Classes: GlassdoorExtensionTests
  - Tests: ~2
  - Dependencies: Microsoft.Extensions.DependencyInjection, ServiceCollection, Microsoft.Extensions.Configuration, ConfigurationBuilder

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorApiClientTests.cs`
  - Classes: GlassdoorApiClientTests
  - Tests: ~21
  - Dependencies: Ghost.Platform.Glassdoor.Internal

- `tests/Contracts/Ghost.Contracts.Tests/IExtensionTests.cs`
  - Classes: IExtensionTests
  - Tests: ~1
  - Dependencies: ServiceCollection, Microsoft.Extensions.Configuration, Version, Microsoft.Extensions.DependencyInjection, FakeService

- `tests/Contracts/Ghost.Contracts.Inference.Tests/InferenceResponseTests.cs`
  - Classes: InferenceResponseTests
  - Tests: ~2
  - Dependencies: Ghost.Contracts.Inference, InferenceResponse

- `tests/Contracts/Ghost.Contracts.Inference.Tests/InferenceRoleTests.cs`
  - Classes: InferenceRoleTests
  - Tests: ~1
  - Dependencies: Ghost.Contracts.Inference

- `tests/Contracts/Ghost.Contracts.Inference.Tests/TokenUsageTests.cs`
  - Classes: TokenUsageTests
  - Tests: ~2
  - Dependencies: Ghost.Contracts.Inference, TokenUsage

- `tests/Contracts/Ghost.Contracts.Inference.Tests/InferenceChunkTests.cs`
  - Classes: InferenceChunkTests
  - Tests: ~2
  - Dependencies: Ghost.Contracts.Inference, InferenceChunk

- `tests/Contracts/Ghost.Contracts.Inference.Tests/InferenceRequestTests.cs`
  - Classes: InferenceRequestTests
  - Tests: ~3
  - Dependencies: Ghost.Contracts.Inference, InferenceRequest

- `tests/Contracts/Ghost.Contracts.Inference.Tests/InferenceMessageTests.cs`
  - Classes: InferenceMessageTests
  - Tests: ~2
  - Dependencies: Ghost.Contracts.Inference, InferenceMessage

- `tests/Hosting/Ghost.Hosting.Tests/ExtensionDependencyFilteringTests.cs`
  - Classes: ExtensionDependencyFilteringTests
  - Tests: ~6
  - Dependencies: MockJobScraper1, ServiceCollection, Microsoft.Extensions.Configuration, System.Linq, Microsoft.Extensions.DependencyInjection

- `tests/Hosting/Ghost.Hosting.Tests/ExtensionExceptionTests.cs`
  - Classes: ExtensionExceptionTests
  - Tests: ~2
  - Dependencies: ExtensionException

- `tests/Hosting/Ghost.Hosting.Tests/ServiceCollectionExtensionsTests.cs`
  - Classes: ServiceCollectionExtensionsTests
  - Tests: ~5
  - Dependencies: ServiceCollection, Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, ConfigurationBuilder, System.Collections.Generic

- `tests/Hosting/Ghost.Hosting.Tests/GhostwriterBuilderTests.cs`
  - Classes: GhostBuilderTests
  - Tests: ~3
  - Dependencies: Microsoft.Extensions.DependencyInjection, ServiceCollection, Ghost.Hosting.Tests.Helpers

- `tests/Hosting/Ghost.Hosting.Tests/ExtensionLoaderTests.cs`
  - Classes: ExtensionLoaderTests
  - Tests: ~4
  - Dependencies: Circular1, Circular2, Microsoft.Extensions.Configuration, MockMissingDepExtension, ServiceCollection

- `tests/Hosting/Ghost.Hosting.Tests/GhostKernelHostedServiceTests.cs`
  - Classes: GhostKernelHostedServiceTests
  - Tests: ~2
  - Dependencies: bool, Microsoft.Playwright, Ghost, CancellationTokenSource, System.Threading

- `tests/Hosting/Ghost.Hosting.Tests/GhostwriterOptionsTests.cs`
  - Classes: GhostOptionsTests
  - Tests: ~3
  - Dependencies: GhostOptions

- `tests/Ghost.WebApi.Tests/Features/Health/HealthEndpointsIntegrationTests.cs`
  - Classes: HealthEndpointsIntegrationTests
  - Tests: ~16
  - Dependencies: Microsoft.AspNetCore.Http, DefaultHttpContext, HttpRequestException, System.Net.Http, System.Text

- `tests/Ghost.Platform.Indeed.Tests/IndeedExtensionTests.cs`
  - Classes: IndeedExtensionTests
  - Tests: ~1
  - Dependencies: LoggerFactory, ServiceCollection, Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, ConfigurationBuilder

- `tests/Ghost.Platform.Indeed.Tests/IndeedJobParserTests.cs`
  - Classes: IndeedJobParserTests
  - Tests: ~1
  - Dependencies: Ghost.Platform.Indeed.Internal, System.Text.Json

- `tests/Ghost.Platform.Indeed.Tests/IndeedJobClientParallelTests.cs`
  - Classes: IndeedJobClientParallelTests
  - Tests: ~1
  - Dependencies: System.Net.Http, Microsoft.Extensions.Logging.Abstractions, StringContent, System.Threading, IndeedApiClient

- `tests/Ghost.Platform.Indeed.Tests/IndeedHtmlParsingTests.cs`
  - Classes: IndeedHtmlParsingTests
  - Tests: ~4
  - Dependencies: System.Diagnostics, Ghost.Platform.Indeed.Internal

- `tests/Ghost.Platform.Indeed.Tests/IndeedApiClientMetricsTests.cs`
  - Classes: IndeedApiClientMetricsTests
  - Tests: ~7
  - Dependencies: Ghost.Models, BlockingHandler, StubProxyProvider, ResponseHandler, System.Net.Http

- `tests/Ghost.Core.Tests/DateParserTests.cs`
  - Classes: DateParserTests
  - Tests: ~3
  - Dependencies: Ghost.Utilities

- `tests/Ghost.Core.Tests/JsonLdExtractorTests.cs`
  - Classes: JsonLdExtractorTests
  - Tests: ~2
  - Dependencies: Ghost.Utilities, System.Text.Json

- `tests/Ghost.Core.Tests/CircuitBreakerTests.cs`
  - Classes: CircuitBreakerTests
  - Tests: ~13
  - Dependencies: CircuitBreakerOptions, Ghost.Resilience, CircuitBreaker, InvalidOperationException

- `tests/Ghost.Core.Tests/DeduplicationServiceTests.cs`
  - Classes: DeduplicationServiceTests
  - Tests: ~2
  - Dependencies: Ghost.Utilities

- `tests/SDK/Ghost.Sdk.Spider.Tests/Integration/GraphQLPaginationTests.cs`
  - Classes: GraphQLPaginationTests
  - Tests: ~8
  - Dependencies: Request, Ghost.Sdk.Spider.Adapters.GraphQL, StringContent, GraphQLAdapter, Microsoft.Extensions.Logging

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Entities/EntityParserTests.cs`
  - Classes: EntityParserTests
  - Tests: ~12
  - Dependencies: Ghost.Sdk.Spider.Tests.TestHelpers, Ghost.Sdk.Spider.Core.Extraction

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Entities/FormatterTests.cs`
  - Classes: FormatterTests, TrimFormatterTests, HtmlDecodeFormatterTests, UrlDecodeFormatterTests, ReplaceFormatterTests, RegexFormatterTests, DateTimeFormatterTests, StringFormatterTests, FormatterChainTests
  - Tests: ~29
  - Dependencies: System.Web, TrimFormatter, HtmlDecodeFormatter, UrlDecodeFormatter, DateTimeFormatter

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Entities/EntityBaseTests.cs`
  - Classes: EntityBaseTests
  - Tests: ~10
  - Dependencies: DateTime, Ghost.Sdk.Spider.Core.Entities, Ghost.Sdk.Spider.Tests.TestHelpers

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Entities/FormatterImplementationTests.cs`
  - Classes: FormatterImplementationTests, TrimFormatterTests, RegexFormatterTests, DateTimeFormatterTests, HtmlDecodeFormatterTests, StringFormatterTests, ReplaceFormatterTests, UrlDecodeFormatterTests, FormatterChainTests
  - Tests: ~32
  - Dependencies: System.Globalization, DateTime, TrimFormatter, HtmlDecodeFormatter, UrlDecodeFormatter

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/PostgreSqlStorageTests.cs`
  - Classes: PostgreSqlStorageTests
  - Tests: ~16
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, MockPostgreSqlStorage, Ghost.Sdk.Spider.Configuration.Models, InvalidOperationException, string

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/WebhookStorageAdvancedTests.cs`
  - Classes: WebhookStorageAdvancedTests
  - Tests: ~12
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, Microsoft.Extensions.Logging.Abstractions, Ghost.Sdk.Spider.Storage.Sinks, CancellationTokenSource, HttpClient

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/IStorageContractTests.cs`
  - Classes: IStorageContractTests
  - Tests: ~5
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, CancellationTokenSource, MockStorage

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/ConsoleStorageTests.cs`
  - Classes: ConsoleStorageTests
  - Tests: ~9
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, StringWriter, System.Text, Microsoft.Extensions.Logging.Abstractions, Ghost.Sdk.Spider.Storage.Sinks

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/WebhookStorageDetailedTests.cs`
  - Classes: WebhookStorageDetailedTests
  - Tests: ~10
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, System.Text, Microsoft.Extensions.Logging.Abstractions, Ghost.Sdk.Spider.Storage.Sinks, HttpClient

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/StorageSinksTests.cs`
  - Classes: StorageSinksTests
  - Tests: ~18
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, System.Net.Http, Ghost.Sdk.Spider.Storage.Sinks, CancellationTokenSource, ConsoleStorage

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/StorageResultTests.cs`
  - Classes: StorageResultTests
  - Tests: ~6
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/StoragePipelineTests.cs`
  - Classes: StoragePipelineTests
  - Tests: ~13
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, CancellationTokenSource, MockStorage, StoragePipeline

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/TransformationTests.cs`
  - Classes: TransformationTests
  - Tests: ~13
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, GeocodeTransformation, DeduplicationTransformation, NormalizeTransformation, FilterTransformation

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/StoragePipelineFullTests.cs`
  - Classes: StoragePipelineFullTests
  - Tests: ~17
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, InvalidOperationException, MockStorage, StoragePipeline

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/BatchProcessingTests.cs`
  - Classes: BatchProcessingTests
  - Tests: ~8
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, System.Collections.Concurrent, Microsoft.Extensions.Logging.Abstractions, Ghost.Sdk.Spider.Storage.Sinks, ConsoleStorage

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/WebhookStorageTests.cs`
  - Classes: WebhookStorageTests
  - Tests: ~14
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, HttpRequestException, Microsoft.Extensions.Logging.Abstractions, Ghost.Sdk.Spider.Storage.Sinks, StringContent

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/ConsoleStorageAdvancedTests.cs`
  - Classes: ConsoleStorageAdvancedTests
  - Tests: ~16
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, StringWriter, DateTime, System.Text, Microsoft.Extensions.Logging.Abstractions

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/ElasticsearchStorageTests.cs`
  - Classes: ElasticsearchStorageTests
  - Tests: ~19
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts, System.Globalization, DateTimeOffset, Ghost.Sdk.Spider.Configuration.Models, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Storage/StorageContextTests.cs`
  - Classes: StorageContextTests
  - Tests: ~10
  - Dependencies: Ghost.Sdk.Spider.Storage.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/SpiderStateBoxTests.cs`
  - Classes: SpiderStateBoxTests
  - Tests: ~22
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/PipelineExecutionTests.cs`
  - Classes: PipelineExecutionTests
  - Tests: ~10
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, Ghost.Sdk.Spider.Pipeline.Contracts, TestMiddleware, object

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/PipelineTests.cs`
  - Classes: PipelineTests
  - Tests: ~15
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, MiddlewareConfiguration, Ghost.Sdk.Spider.Pipeline.Contracts, object

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/PipelineBuilderTests.cs`
  - Classes: PipelineBuilderTests
  - Tests: ~18
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, Ghost.Sdk.Spider.Pipeline.Contracts, Ghost.Sdk.Spider.Adapters.Contracts, PipelineBuilder

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/CircuitBreakerMiddlewareFullTests.cs`
  - Classes: CircuitBreakerMiddlewareFullTests
  - Tests: ~10
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, CircuitBreakerMiddleware, HttpRequestException, Ghost.Sdk.Spider.Pipeline.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/RetryMiddlewareTests.cs`
  - Classes: RetryMiddlewareTests
  - Tests: ~12
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, HttpRequestException, Ghost.Sdk.Spider.Pipeline.Contracts, RetryMiddleware

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/ProxyRotationMiddlewareTests.cs`
  - Classes: ProxyRotationMiddlewareTests
  - Tests: ~11
  - Dependencies: Ghost.Sdk.Spider.Pipeline, HttpRequestException, Ghost.Sdk.Spider.Pipeline.Contracts, ProxyRotationMiddleware, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/StealthMiddlewareTests.cs`
  - Classes: StealthMiddlewareTests
  - Tests: ~14
  - Dependencies: Ghost.Sdk.Spider.Pipeline, Ghost.Sdk.Spider.Pipeline.Contracts, StealthMiddleware, CancellationTokenSource, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/StealthMiddlewareFullTests.cs`
  - Classes: StealthMiddlewareFullTests
  - Tests: ~11
  - Dependencies: Ghost.Sdk.Spider.Pipeline, Ghost.Sdk.Spider.Pipeline.Contracts, StealthMiddleware, Ghost.Sdk.Spider.Adapters.Contracts, Ghost.Sdk.Spider.Pipeline.Middleware

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/RateLimitMiddlewareTests.cs`
  - Classes: RateLimitMiddlewareTests
  - Tests: ~7
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, Ghost.Sdk.Spider.Pipeline.Contracts, RateLimitMiddleware, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Pipeline/Middleware/CircuitBreakerMiddlewareTests.cs`
  - Classes: CircuitBreakerMiddlewareTests
  - Tests: ~9
  - Dependencies: SpiderStateBox, Ghost.Sdk.Spider.Pipeline, CircuitBreakerMiddleware, Ghost.Sdk.Spider.Pipeline.Contracts, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Scheduling/TriggerManagerTests.cs`
  - Classes: TriggerManagerTests
  - Tests: ~7
  - Dependencies: SpiderOptions, Ghost.Sdk.Spider.Scheduling.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Scheduling/SpiderJobTests.cs`
  - Classes: SpiderJobTests
  - Tests: ~14
  - Dependencies: JobKey, Ghost.Sdk.Spider.Engine, Ghost.Sdk.Spider.Tests.TestHelpers, CancellationTokenSource, JobDataMap

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Scheduling/QuartzSpiderSchedulerTests.cs`
  - Classes: QuartzSpiderSchedulerTests
  - Tests: ~18
  - Dependencies: ArgumentException, CancellationTokenSource, Ghost.Sdk.Spider.Scheduling.Contracts, KeyNotFoundException, OperationCanceledException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Scheduling/DistributedLockTests.cs`
  - Classes: DistributedLockTests
  - Tests: ~8
  - Dependencies: SemaphoreSlim, System.Collections.Concurrent, object, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/SpiderTests.cs`
  - Classes: SpiderTests
  - Tests: ~14
  - Dependencies: ConfigurableTestSpider, SpiderExecutionContext, Ghost.Sdk.Spider.Engine, Ghost.Sdk.Spider.Tests.TestHelpers, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/InMemoryRequestQueueTests.cs`
  - Classes: InMemoryRequestQueueTests
  - Tests: ~19
  - Dependencies: InMemoryRequestQueue, Ghost.Sdk.Spider.Adapters.Contracts, Ghost.Sdk.Spider.Engine.Queue

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/SpiderOrchestratorTests.cs`
  - Classes: SpiderOrchestratorTests
  - Tests: ~8
  - Dependencies: ConfigurableTestSpider, SpiderExecutionContext, Ghost.Sdk.Spider.Engine, HttpRequestException, Ghost.Sdk.Spider.Tests.TestHelpers

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/SpiderEngineTests.cs`
  - Classes: SpiderEngineTests
  - Tests: ~27
  - Dependencies: ConfigurableTestSpider, SpiderExecutionContext, Ghost.Sdk.Spider.Engine.Queue, Response, Ghost.Sdk.Spider.Engine

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/ExecutionContextTests.cs`
  - Classes: ExecutionContextTests
  - Tests: ~41
  - Dependencies: Ghost.Sdk.Spider.Engine, SpiderExecutionContext, System.Collections.Concurrent, System.Globalization

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/ParallelExecutionTests.cs`
  - Classes: ParallelExecutionTests
  - Tests: ~8
  - Dependencies: ConfigurableTestSpider, SemaphoreSlim, System.Collections.Concurrent, Ghost.Sdk.Spider.Engine, ArgumentException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Engine/RequestQueueTests.cs`
  - Classes: RequestQueueTests
  - Tests: ~17
  - Dependencies: MockDistributedQueue, InMemoryRequestQueue, Ghost.Sdk.Spider.Adapters.Contracts, Ghost.Sdk.Spider.Engine.Queue

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Configuration/ConfigurationCompilerTests.cs`
  - Classes: ConfigurationCompilerTests
  - Tests: ~20
  - Dependencies: Ghost.Sdk.Spider.Configuration.Compiler, ConfigurationCompiler, Ghost.Sdk.Spider.Configuration.Models, Ghost.Sdk.Spider.Configuration

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Configuration/ConfigurationModelsTests.cs`
  - Classes: ConfigurationModelsTests
  - Tests: ~35
  - Dependencies: MonitoringConfiguration, ScheduleConfiguration, AlertRuleConfiguration, LoggingConfiguration, StorageConfiguration

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Configuration/ComplexValidationTests.cs`
  - Classes: ComplexValidationTests
  - Tests: ~8
  - Dependencies: Ghost.Sdk.Spider.Configuration.Models

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/GraphQLModelsTests.cs`
  - Classes: GraphQLModelsTests
  - Tests: ~29
  - Dependencies: GraphQLRequest, GraphQLResponse, Ghost.Sdk.Spider.Adapters.GraphQL

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/AdapterFactoryImplementationTests.cs`
  - Classes: AdapterFactoryImplementationTests
  - Tests: ~9
  - Dependencies: AdapterRegistry, ServiceCollection, TestAdapter, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/GraphQLSchemaTests.cs`
  - Classes: GraphQLSchemaTests
  - Tests: ~37
  - Dependencies: Ghost.Sdk.Spider.Adapters.GraphQL.Schema, GraphQLSchema

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/JavaScriptAdapterTests.cs`
  - Classes: JavaScriptAdapterTests
  - Tests: ~17
  - Dependencies: Request, Microsoft.Playwright, CancellationTokenSource, Microsoft.Extensions.Logging, Ghost.Sdk.Spider.Adapters

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/JavaScriptAdapterMockTests.cs`
  - Classes: JavaScriptAdapterMockTests
  - Tests: ~14
  - Dependencies: Request, Microsoft.Playwright, Microsoft.Extensions.Logging, Ghost.Sdk.Spider.Adapters, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/JavaScriptAdapterFullTests.cs`
  - Classes: JavaScriptAdapterFullTests
  - Tests: ~16
  - Dependencies: Request, JavaScriptAdapterOptions, Microsoft.Extensions.Logging, Ghost.Sdk.Spider.Adapters, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/HttpRequestBuilderTests.cs`
  - Classes: HttpRequestBuilderTests
  - Tests: ~29
  - Dependencies: Request, System.Net.Http, Uri, HttpRequestBuilder, Ghost.Sdk.Spider.Adapters

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/MessageBufferTests.cs`
  - Classes: MessageBufferTests
  - Tests: ~38
  - Dependencies: Ghost.Sdk.Spider.Adapters.WebSocket, WebSocketMessage, MessageBuffer, System.Net.WebSockets

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/GraphQLAdapterExtractTests.cs`
  - Classes: GraphQLAdapterExtractTests
  - Tests: ~31
  - Dependencies: Request, GraphQLAdapterOptions, HttpRequestException, System.Net.Http, Ghost.Sdk.Spider.Adapters.GraphQL

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/WebSocketAdapterMockTests.cs`
  - Classes: WebSocketAdapterMockTests
  - Tests: ~22
  - Dependencies: Ghost.Sdk.Spider.Adapters.WebSocket, ReconnectionPolicy, HeartbeatOptions, MessageBuffer, System.Net.WebSockets

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/GraphQLAdapterComprehensiveTests.cs`
  - Classes: GraphQLAdapterComprehensiveTests
  - Tests: ~17
  - Dependencies: Request, HttpRequestException, StringContent, GraphQLAdapter, Microsoft.Extensions.Logging

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/StaticHtmlAdapterTests.cs`
  - Classes: StaticHtmlAdapterTests
  - Tests: ~14
  - Dependencies: Ghost.Sdk.Spider.Tests.TestHelpers, Microsoft.Extensions.Logging.Abstractions, Uri, CancellationTokenSource, Ghost.Sdk.Spider.Adapters

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/GraphQLPaginationTests.cs`
  - Classes: GraphQLPaginationTests
  - Tests: ~10
  - Dependencies: Ghost.Sdk.Spider.Adapters.GraphQL, Microsoft.Extensions.Logging, Ghost.Sdk.Spider.Adapters, System.Net, Ghost.Sdk.Spider.Adapters.Contracts

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/AdapterRegistryTests.cs`
  - Classes: AdapterRegistryTests
  - Tests: ~33
  - Dependencies: AdapterRegistry, IContentAdapter, Ghost.Sdk.Spider.Adapters, string, StaticHtmlAdapter

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/AdapterFactoryTests.cs`
  - Classes: AdapterFactoryTests
  - Tests: ~15
  - Dependencies: AdapterRegistry, ServiceCollection, NullLogger, Microsoft.Extensions.DependencyInjection, Ghost.Sdk.Spider.Tests.TestHelpers

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/ReconnectionPolicyTests.cs`
  - Classes: ReconnectionPolicyTests
  - Tests: ~21
  - Dependencies: Ghost.Sdk.Spider.Adapters.WebSocket, ReconnectionPolicy

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Adapters/RequestResponseComprehensiveTests.cs`
  - Classes: RequestResponseComprehensiveTests
  - Tests: ~26
  - Dependencies: Ghost.Sdk.Spider.Adapters.Contracts, Request, Response, InvalidOperationException

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/SelectorImplementationTests.cs`
  - Classes: SelectorImplementationTests, XPathSelectorTests, CssSelectorTests, RegexSelectorTests, JsonPathSelectorTests, JmesPathSelectorTests
  - Tests: ~26
  - Dependencies: System.Text.RegularExpressions, CssSelector, RegexSelector, XPathSelector, JmesPathSelector

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/XPathSelectorTests.cs`
  - Classes: XPathSelectorTests
  - Tests: ~11
  - Dependencies: Ghost.Sdk.Spider.Core.Extraction.Selectors, XPathSelector

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/RegexSelectorTests.cs`
  - Classes: RegexSelectorTests
  - Tests: ~18
  - Dependencies: Ghost.Sdk.Spider.Core.Extraction.Selectors, System.Text.RegularExpressions, RegexSelector

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/EntityParserBoostTests.cs`
  - Classes: EntityParserBoostTests
  - Tests: ~21
  - Dependencies: Ghost.Sdk.Spider.Tests.TestHelpers, Ghost.Sdk.Spider.Core.Extraction

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/JsonPathSelectorTests.cs`
  - Classes: JsonPathSelectorTests
  - Tests: ~17
  - Dependencies: Ghost.Sdk.Spider.Core.Extraction.Selectors, Ghost.Sdk.Spider.Tests.TestHelpers, JsonPathSelector

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/CssSelectorTests.cs`
  - Classes: CssSelectorTests
  - Tests: ~14
  - Dependencies: Ghost.Sdk.Spider.Core.Extraction.Selectors, CssSelector

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Extraction/EntityParserImplementationTests.cs`
  - Classes: EntityParserImplementationTests
  - Tests: ~14
  - Dependencies: Ghost.Sdk.Spider.Core.Entities.Attributes, DateTime, Ghost.Sdk.Spider.Core.Entities, Ghost.Sdk.Spider.Core.Extraction

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Strategies/StrategyRouterTests.cs`
  - Classes: StrategyRouterTests
  - Tests: ~13
  - Dependencies: StrategyRouter, Ghost.Sdk.Spider.Strategies, Microsoft.Extensions.Logging.Abstractions

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Strategies/ConditionEvaluatorAdditionalTests.cs`
  - Classes: ConditionEvaluatorAdditionalTests
  - Tests: ~28
  - Dependencies: ConditionEvaluator, Ghost.Sdk.Spider.Strategies

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Strategies/ConditionEvaluatorTests.cs`
  - Classes: ConditionEvaluatorTests
  - Tests: ~20
  - Dependencies: ConditionEvaluator, Ghost.Sdk.Spider.Strategies, TimeoutException

## Integration Tests (17 files)

- `tests/Core/Ghost.Tests/Pool/TieredBrowserPoolTests.cs`
  - Classes: TieredBrowserPoolTests
  - Tests: ~15
  - Dependencies: Ghost.Pool, System.Diagnostics.CodeAnalysis, System.Diagnostics, Microsoft.Extensions.Logging.Abstractions, TieredBrowserPool

- `tests/Core/Ghost.Tests/Integration/GhostKernelIntegrationTests.cs`
  - Classes: GhostKernelIntegrationTests
  - Tests: ~1
  - Dependencies: System.Threading.Tasks, Ghost.Core

- `tests/Core/Ghost.Tests/Services/ErrorCategorizationServiceIntegrationTests.cs`
  - Classes: ErrorCategorizationServiceIntegrationTests
  - Tests: ~25
  - Dependencies: UnauthorizedAccessException, HttpRequestException, ArgumentException, System.Net.Http, System.Net

- `tests/Core/Ghost.Tests/Proxy/ProxyHealthCheckerIntegrationTests.cs`
  - Classes: ProxyHealthCheckerIntegrationTests
  - Tests: ~3
  - Dependencies: TcpListener, ProxyHealthChecker, System.Net.Http, Microsoft.Extensions.Logging.Abstractions, HttpClient

- `tests/Platforms/Ghost.Platform.Google.Integration/GoogleJobsIntegrationTests.cs`
  - Classes: GoogleJobsIntegrationTests
  - Tests: ~8
  - Dependencies: Microsoft.Extensions.DependencyInjection, Ghost.Platform.Google.Jobs, Ghost.Contracts.Jobs, Ghost.Platform.Google.Integration.Fixtures

- `tests/Platforms/Ghost.Platform.Glassdoor.Integration/GlassdoorIntegrationTests.cs`
  - Classes: GlassdoorIntegrationTests
  - Tests: ~8
  - Dependencies: Ghost.Platform.Glassdoor.Integration.Fixtures, Microsoft.Extensions.DependencyInjection, Ghost.Contracts.Jobs

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInEntityTests.cs`
  - Classes: LinkedInEntityTests
  - Tests: ~17
  - Dependencies: LinkedInJobEntity, Ghost.Sdk.Spider.Core.Extraction, FileNotFoundException, Ghost.Platform.LinkedIn.Tests.Migration

- `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInSpiderTests.cs`
  - Classes: LinkedInSpiderTests
  - Tests: ~23
  - Dependencies: LinkedInSpider, Response, Ghost.Sdk.Spider.Engine, ExecutionContext, Ghost.Platform.LinkedIn.Tests.Migration

- `tests/Platforms/Ghost.Platform.Indeed.Integration/IndeedIntegrationTests.cs`
  - Classes: IndeedIntegrationTests
  - Tests: ~7
  - Dependencies: Ghost.Platform.Indeed.Integration.Fixtures, Microsoft.Extensions.DependencyInjection, Ghost.Contracts.Jobs

- `tests/Platforms/Ghost.Platform.LinkedIn.Integration/LinkedInIntegrationTests.cs`
  - Classes: LinkedInIntegrationTests
  - Tests: ~7
  - Dependencies: Ghost.Contracts.Jobs, Random

- `tests/Platforms/Ghost.Platform.InfoJobs.Integration/InfoJobsIntegrationTests.cs`
  - Classes: InfoJobsIntegrationTests
  - Tests: ~9
  - Dependencies: Microsoft.Extensions.DependencyInjection, Ghost.Contracts.Jobs, Ghost.Platform.InfoJobs.Jobs, Ghost.Platform.InfoJobs.Integration.Fixtures

- `tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorJobParserIntegrationTests.cs`
  - Classes: GlassdoorJobParserIntegrationTests
  - Tests: ~19
  - Dependencies: Ghost.Contracts.Jobs, Ghost.Platform.Glassdoor.Internal

- `tests/Integration/RockSolid50KIntegrationTests.cs`
  - Classes: RockSolid50KIntegrationTests
  - Tests: ~8
  - Dependencies: LinkedInSessionPool, RetryPolicy, MemoryFileHybridCache, HttpRequestException, Microsoft.Extensions.Logging.Abstractions

- `tests/SDK/Ghost.Sdk.Spider.Tests/Integration/WebSocketAdapterTests.cs`
  - Classes: WebSocketAdapterTests
  - Tests: ~17
  - Dependencies: Ghost.Sdk.Spider.Adapters.Contracts, WebSocketAdapter

- `tests/SDK/Ghost.Sdk.Spider.Tests/Integration/StaticHtmlAdapterTests.cs`
  - Classes: StaticHtmlAdapterTests
  - Tests: ~21
  - Dependencies: Uri, Microsoft.Extensions.Logging.Abstractions, CancellationTokenSource, Ghost.Sdk.Spider.Adapters, StaticHtmlAdapter

- `tests/SDK/Ghost.Sdk.Spider.Tests/Integration/GraphQLAdapterTests.cs`
  - Classes: GraphQLAdapterTests
  - Tests: ~16
  - Dependencies: Ghost.Sdk.Spider.Adapters.GraphQL, Microsoft.Extensions.Logging.Abstractions, GraphQLAdapter, HttpClient, Ghost.Sdk.Spider.Adapters

- `tests/SDK/Ghost.Sdk.Spider.Tests/Unit/Configuration/ConfigurationLoaderTests.cs`
  - Classes: ConfigurationLoaderTests
  - Tests: ~15
  - Dependencies: ConfigurationLoader, Ghost.Sdk.Spider.Tests.TestHelpers, CancellationTokenSource, Ghost.Sdk.Spider.Configuration

## E2E Tests (2 files)

- `tests/Platforms/Ghost.Platform.X.E2E/XSimulationE2ETests.cs`
  - Classes: XSimulationE2ETests
  - Tests: ~20
  - Dependencies: Microsoft.Extensions.DependencyInjection, Ghost.Platform.X.Internal, Ghost.Contracts.Simulation, Ghost.Platform.X.E2E.Fixtures, Ghost.Contracts.Social

- `tests/Platforms/Ghost.Platform.X.E2E/XPlatformE2ETests.cs`
  - Classes: XPlatformE2ETests
  - Tests: ~17
  - Dependencies: Microsoft.Extensions.DependencyInjection, Ghost.Platform.X.Internal, Ghost.Platform.X.E2E.Fixtures, Ghost.Contracts.Social, string

