# Ghost.Sdk.Spider Implementation Plan
**Date**: 2026-02-04  
**Status**: Approved for Implementation  
**Target**: Complete port of DotnetSpider with enterprise enhancements

---

## Executive Summary

Transform Ghost into a domain-agnostic, enterprise-grade scraping SDK with:
- ✅ ALL DotnetSpider features (entities, formatters, pipelines, storage)
- ✅ Declarative YAML/JSON configuration
- ✅ Imperative code extensibility
- ✅ Multi-strategy fallbacks
- ✅ SPA/PWA/Web App support via Playwright
- ✅ Native WebSocket real-time scraping
- ✅ Full GraphQL support (introspection, pagination, subscriptions)
- ✅ 10,000+ concurrent scraping capacity
- ✅ Zero-allocation hot path with struct contexts
- ✅ 80%+ unit test coverage

---

## Architecture Decisions

### 1. Namespace
**Ghost.Sdk.Spider** - Part of Ghost.Sdk family

### 2. State Management
Struct + Optional StateBox for maximum performance:
```csharp
public readonly struct PipelineContext
{
    public readonly long RequestId;
    public readonly Request Request;
    public readonly SpiderStateBox? State; // null for simple spiders
}
```

### 3. WebSocket
Native WebSocket (not SignalR) for maximum compatibility and control

### 4. Configuration
Both YAML and JSON fully supported

### 5. Scheduling
Quartz.NET integration throughout

### 6. Script Injection
Deferred as future optional plugin

### 7. HTTP Files
`/http/` at repository root with subfolders by component

---

## Phase 1: Foundation (Week 1-2)

### Track A: Core Entity System
**Dependencies**: None

Deliverables:
- Port DotnetSpider entity model
- EntityBase<T> with full attribute support
- ValueSelector, EntitySelector attributes
- All formatters (Trim, Replace, Regex, DateTime, StringFormat)
- EntityParser with XPath, CSS, Regex, JSONPath, JMESPath support
- Unit tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Core/Entities/EntityBase.cs
src/SDK/Ghost.Sdk.Spider/Core/Entities/Attributes/
src/SDK/Ghost.Sdk.Spider/Core/Entities/Formatters/
src/SDK/Ghost.Sdk.Spider/Core/Extraction/EntityParser.cs
src/SDK/Ghost.Sdk.Spider/Core/Extraction/Selectors/
```

### Track B: Configuration Layer
**Dependencies**: None

Deliverables:
- YAML/JSON configuration binding
- SpiderConfiguration model
- Configuration validation (FluentValidation)
- Configuration compiler (YAML → C# objects)
- Unit tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Configuration/
src/SDK/Ghost.Sdk.Spider/Configuration/Models/
src/SDK/Ghost.Sdk.Spider/Configuration/Validation/
src/SDK/Ghost.Sdk.Spider/Configuration/Compiler/
```

### Track C: Content Adapters (Interfaces)
**Dependencies**: None

Deliverables:
- IContentAdapter interface
- ContentResult model
- AdapterFactory
- Request/Response models
- Unit tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Adapters/
src/SDK/Ghost.Sdk.Spider/Adapters/Contracts/
```

### Track D: Documentation Structure
**Dependencies**: None

Deliverables:
- /http/ folder structure
- API surface documentation template
- HTTP file examples

Files:
```
/http/spider/
/http/adapters/
/docs/spider/
```

---

## Phase 2: Adapter Implementation (Week 3-4)

### Track A: StaticHtmlAdapter
**Dependencies**: Phase 1 Track A, Track C

Deliverables:
- HTTP client integration
- Header/cookie management
- Response handling
- Compression support
- Unit + integration tests (80%+ coverage)

### Track B: JavaScriptAdapter
**Dependencies**: Phase 1 Track A, Track C

Deliverables:
- Playwright browser pool integration
- Script execution
- Wait conditions
- Request interception
- Screenshot capture
- Unit + integration tests (80%+ coverage)

### Track C: GraphQLAdapter
**Dependencies**: Phase 1 Track A, Track C

Deliverables:
- Schema introspection
- Query validation
- Relay-style pagination (automatic)
- Subscriptions via WebSocket
- Variable substitution
- Unit + integration tests (80%+ coverage)

### Track D: WebSocketAdapter
**Dependencies**: Phase 1 Track A, Track C

Deliverables:
- Native WebSocket client
- Connection management
- Message aggregation (JSON array)
- Timeout handling
- Real-time streaming support
- Unit + integration tests (80%+ coverage)

---

## Phase 3: Pipeline & Execution (Week 5-6)

### Track A: Middleware Pipeline
**Dependencies**: Phase 2 complete

Deliverables:
- Compiled pipeline with struct contexts
- IPipelineMiddleware interface
- Pipeline compilation (expression trees)
- StateBox for complex state
- Built-in middlewares:
  - ProxyRotationMiddleware
  - RateLimitMiddleware (TokenBucket)
  - StealthMiddleware (fingerprint, timezone)
  - CircuitBreakerMiddleware
  - RetryMiddleware
  - DeduplicationMiddleware
  - ConsentHandlerMiddleware
  - ValidationMiddleware
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Pipeline/
src/SDK/Ghost.Sdk.Spider/Pipeline/Middleware/
src/SDK/Ghost.Sdk.Spider/Pipeline/Compilation/
```

### Track B: Strategy Router
**Dependencies**: Phase 2 complete

Deliverables:
- Multi-strategy fallback system
- IStrategyRouter interface
- Condition evaluation engine
- StrategyContext
- StrategyAttempt tracking
- Metrics collection
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Strategies/
```

### Track C: Entity Parser Enhancement
**Dependencies**: Phase 1 Track A

Deliverables:
- XPath selector engine
- CSS selector engine
- Regex selector engine
- JSONPath selector engine
- JMESPath selector engine
- Nested entity support
- Async formatter support
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Core/Extraction/Selectors/
```

### Track D: Scheduler
**Dependencies**: Phase 3 Track A

Deliverables:
- Quartz.NET integration
- IScheduler interface
- Cron expression support
- Job persistence
- Distributed locking
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Scheduling/
```

---

## Phase 4: Integration & Polish (Week 7-8)

### Track A: Spider Engine
**Dependencies**: Phase 3 complete

Deliverables:
- ISpiderEngine interface
- Spider orchestration
- IAsyncEnumerable streaming
- Request queue management
- Parallel execution control
- Error handling & recovery
- Metrics & logging
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Engine/
```

### Track B: Storage Pipeline
**Dependencies**: Phase 3 complete

Deliverables:
- IStorage interface
- Pipeline sink architecture
- PostgreSQL storage
- Elasticsearch storage
- Webhook storage
- Transformation pipeline
- Batch processing
- Unit + integration tests (80%+ coverage)

Files:
```
src/SDK/Ghost.Sdk.Spider/Storage/
src/SDK/Ghost.Sdk.Spider/Storage/Sinks/
src/SDK/Ghost.Sdk.Spider/Storage/Transformations/
```

### Track C: HTTP Files
**Dependencies**: Phase 4 Track A

Deliverables:
- Complete API documentation
- Executable HTTP files for all endpoints
- Example spiders (YAML)
- Integration test scenarios

Files:
```
/http/spider/*.http
/http/adapters/*.http
/docs/spider/*.md
```

### Track D: Migration
**Dependencies**: All tracks complete

Deliverables:
- Port existing DotnetSpider spiders
- Migrate Indeed, LinkedIn, Glassdoor, GoogleJobs platforms
- Remove all DotnetSpider package references
- Update platform plugins to use new SDK
- Integration tests for each platform

---

## Project Structure

```
src/
  SDK/
    Ghost.Sdk.Spider/
      Ghost.Sdk.Spider.csproj
      Core/
        Entities/
        Extraction/
        Configuration/
      Adapters/
        Contracts/
        StaticHtmlAdapter.cs
        JavaScriptAdapter.cs
        GraphQLAdapter.cs
        WebSocketAdapter.cs
      Pipeline/
        Contracts/
        Middleware/
        Compilation/
        StateBox.cs
      Strategies/
        Contracts/
        StrategyRouter.cs
      Engine/
        SpiderEngine.cs
        SpiderOrchestrator.cs
      Scheduling/
        QuartzScheduler.cs
      Storage/
        Contracts/
        Sinks/
        Transformations/
      Monitoring/
        Metrics.cs
        Tracing.cs
      
tests/
  SDK/
    Ghost.Sdk.Spider.Tests/
      Unit/
      Integration/
      Fixtures/

/http/
  spider/
  adapters/
  examples/

docs/
  spider/
    api/
    configuration/
    examples/
```

---

## Testing Requirements

### Minimum Coverage: 80%

### Test Categories:
1. **Unit Tests**: Individual components, mocked dependencies
2. **Integration Tests**: Component interactions, real (test) services
3. **E2E Tests**: Full spider execution against test servers

### Test Patterns:
```csharp
// Example unit test pattern
[Test]
public async Task EntityParser_ExtractsFields_WithXPathSelector()
{
    // Arrange
    var html = "<div><h1>Title</h1></div>";
    var parser = new EntityParser<TestEntity>();
    
    // Act
    var results = await parser.ParseAsync(html, "http://test.com");
    
    // Assert
    results.Should().HaveCount(1);
    results[0].Title.Should().Be("Title");
}

// Example integration test pattern
[Test]
public async Task StaticHtmlAdapter_FetchesRealPage()
{
    // Arrange
    var adapter = _services.GetRequiredService<StaticHtmlAdapter>();
    var request = new Request { Url = "http://localhost:8080/test" };
    
    // Act
    var result = await adapter.FetchAsync(request, new AdapterOptions(), CancellationToken.None);
    
    // Assert
    result.Success.Should().BeTrue();
    result.Content.Should().Contain("Expected content");
}
```

---

## Dependencies

### NuGet Packages:
```xml
<PackageReference Include="Microsoft.Playwright" Version="1.40.0" />
<PackageReference Include="YamlDotNet" Version="13.7.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="JsonPath.Net" Version="0.7.0" />
<PackageReference Include="JMESPath.NET" Version="1.0.0" />
<PackageReference Include="Quartz" Version="3.8.0" />
<PackageReference Include="Quartz.Extensions.Hosting" Version="3.8.0" />
<PackageReference Include="Polly" Version="8.0.0" />
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.57" />
<PackageReference Include="AngleSharp" Version="1.0.7" />
<PackageReference Include="System.IO.Pipelines" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />

<!-- Testing -->
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="NUnit" Version="4.0.1" />
<PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.0.0" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />
<PackageReference Include="WireMock.Net" Version="1.5.47" />
```

---

## Success Criteria

1. ✅ All DotnetSpider features ported and enhanced
2. ✅ Zero DotnetSpider package references remain
3. ✅ 80%+ unit test coverage
4. ✅ All integration tests passing
5. ✅ HTTP files executable and documented
6. ✅ Existing platforms migrated and working
7. ✅ Performance: 10,000+ concurrent requests supported
8. ✅ Memory: Zero-allocation hot path for simple spiders
9. ✅ Build: Clean compilation, no warnings
10. ✅ Documentation: Complete API docs with examples

---

## Implementation Order

### Week 1:
- Day 1-2: Project structure, Track A (Core Entities) start
- Day 3-4: Track B (Configuration) start
- Day 5: Track C (Adapter interfaces), Track D (Docs structure)

### Week 2:
- Day 1-2: Complete Track A, Track B
- Day 3-5: Complete Track C, start Track D examples

### Week 3-4: Phase 2 - Adapter Implementation
- Parallel implementation of all 4 adapters

### Week 5-6: Phase 3 - Pipeline & Execution
- Parallel implementation of pipeline, router, parser, scheduler

### Week 7-8: Phase 4 - Integration & Polish
- Engine, storage, documentation, migration

---

## Notes

- No shortcuts. Every feature fully implemented.
- Every public API must have XML documentation
- Every component must have comprehensive tests
- HTTP files must be executable (tested)
- Performance benchmarks required for hot paths
- Logging at appropriate levels throughout
- Configuration validation with helpful error messages

---

**Approved by**: Distinguished Engineer  
**Start Date**: 2026-02-04  
**Target Completion**: 2026-04-04 (8 weeks)
