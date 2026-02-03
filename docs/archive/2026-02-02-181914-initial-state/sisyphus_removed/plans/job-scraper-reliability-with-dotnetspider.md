# Job Scraper Reliability Enhancement with DotnetSpider Integration - Final Work Plan

## TL;DR

**Objective**: Fix broken job scrapers for Indeed, Glassdoor, and Google Jobs to achieve near 100% reliability using DotnetSpider integration

**Deliverables**:
- Leverage existing Ghost browser stealth capabilities for anti-bot protection
- Unified session management using SessionFactory across all platforms
- **DotnetSpider integration** for sophisticated scraping with entity-based parsing
- Robust parsing engines with fallback strategies (enhanced by DotnetSpider)
- Intelligent error handling with consent/CAPTCHA management
- Abstract proxy system supporting any residential proxy provider
- Free third-party API fallbacks for ULTRA MISER MODE ($0)

**Estimated Effort**: Large (6-7 weeks)
- **Original estimate**: 5-6 weeks
- **DotnetSpider integration**: Adds ~1 week to schedule

**Parallel Execution**: YES - Platform teams can work in parallel
**Critical Path**: Shared infrastructure → DotnetSpider integration → Platform-specific implementations → Integration testing

---

## DotnetSpider Integration Strategy

### Why DotnetSpider?

Based on analysis of your fork at https://github.com/rudironsoni/DotnetSpider, DotnetSpider provides:

1. **Entity-Based Parsing Model**
   - Declarative data extraction using attributes
   - Built-in selector system (XPath, CSS)
   - Formatters for data transformation
   - Schema management for database generation

2. **Request Scheduling System**
   - Built-in request queue with deduplication
   - Depth control for crawl breadth
   - Retry mechanisms with exponential backoff
   - Request timeout handling

3. **Data Flow Pipeline**
   - Modular processor chain
   - Response delegate pattern
   - Storage abstraction layer
   - Statistics integration

4. **Advanced Features**
   - Distributed scraping support (message queues)
   - Multiple downloader implementations
   - Statistics and monitoring
   - PPPoE downloader for residential proxies

### Integration Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Ghost Application                          │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐    ┌──────────────────────────────────┐    │
│  │ Ghost Kernel │    │   DotnetSpider Integration Layer    │    │
│  │  (Ghost      │◄──►│  ───────────────────────────────── │    │
│  │   Browser)   │    │  │  DotnetSpiderSpiderAdapter │   │    │
│  └──────────────┘    │         (Wraps DotnetSpider)   │    │
│                      └──────────────────────────────────┘    │
│                               │                                │
│                      ┌──────────┴──────────┐                   │
│                      ▼                      ▼                   │
│           ┌─────────────────┐    ┌─────────────────┐            │
│           │  Indeed Spider  │    │ Glassdoor Spider│            │
│           │  (HttpClient)   │    │ (HttpClient +   │            │
│           │                 │    │  Browser Fallback)│            │
│           └─────────────────┘    └─────────────────┘            │
│                      ┌──────────┬──────────┐                 │
│                      ▼          │          ▼                 │
│           ┌─────────────────┐    ┌─────────────────┐            │
│           │Google Jobs Spider│    │  SessionFactory  │            │
│           │(Browser + HTTP) │    │ (Shared Session) │            │
│           └─────────────────┘    └─────────────────┘            │
└─────────────────────────────────────────────────────────────┘
```

### Hybrid Approach

**For Each Platform**:
1. **Existing Code Path**: Keep current API/HTML scraping implementations
2. **DotnetSpider Path**: Use DotnetSpider for data extraction from HTML responses
3. **Integration Point**: DotnetSpider entity parsers work with HTML responses from existing downloaders

**Benefits**:
- Leverage DotnetSpider's selector-based parsing without rewriting downloaders
- Keep existing proxy/stealth integration
- Add entity-based data models with automatic schema generation
- Use DotnetSpider's statistics and retry mechanisms

---

## Work Objectives

### Core Objective
Transform unreliable job scrapers into production-grade, near-100% reliable data collection systems by:
1. Integrating existing infrastructure improvements
2. Adding DotnetSpider's sophisticated data extraction capabilities
3. Creating unified session management
4. Implementing robust error recovery

### Concrete Deliverables

#### Phase 1-2: Foundation & Proxies (From Original Plan)
1. Enhanced Session Management Framework using SessionFactory
2. Abstract Proxy System supporting any residential proxy provider
3. Robust Parsing Engines with multi-strategy approach
4. Intelligent Error Recovery with consent/CAPTCHA management
5. Monitoring & Observability

#### Phase 1.5: DotnetSpider Integration (New)
1. **DotnetSpider Integration Layer**
   - Adapter pattern to integrate DotnetSpider with existing Ghost infrastructure
   - Entity models for each platform (IndeedJob, GlassdoorJob, GoogleJob)
   - Selector-based parsers using XPath/CSS
   - Statistics integration with DotnetSpider's monitoring

2. **DotnetSpider-Enhanced Parsers**
   - Entity-based data extraction models
   - Built-in formatters (trim, replace, regex)
   - FollowRequestSelector for pagination
   - Data validation with attributes

3. **DotnetSpider Features we'll leverage**:
   - Scheduler system for request management
   - RetriedTimes configuration for retry logic
   - Statistics service for success/failure tracking
   - DataFlow pipeline for processing chain
   - Storage adapters for database integration

### Definition of Done (Updated)
- [ ] All three platforms achieve >95% success rate in integration tests
- [ ] <1% error rate from anti-bot detection (blocking, rate limiting)
- [ ] Consent page handling succeeds >90% of the time
- [ ] All scrapers use unified SessionFactory infrastructure
- [ ] DotnetSpider entity parsers deployed for all platforms
- [ ] Comprehensive test coverage for new features
- [ ] Monitoring dashboard operational

### Must Have (Updated)
- Abstract proxy integration supporting any provider
- Proper cookie handling with CookieContainer
- **DotnetSpider entity-based parsers** with selector-based extraction
- Multi-strategy parsers with fallback (DotnetSpider + custom)
- Consent page detection and handling
- Free third-party API fallbacks
- Structured error reporting
- DotnetSpider statistics integration

### Must NOT Have (Guardrails)
- No breaking changes to existing public APIs
- No hardcoded tokens or credentials in code
- No reliance on single proxy provider
- No exceptions that crash the application
- No excessive resource consumption (memory leaks)
- No paid third-party services
- **No replacement of existing Ghost browser infrastructure** - use in parallel

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: YES (test projects exist for all platforms)
- **User wants tests**: TDD (Write tests first, then implementation)
- **Framework**: xUnit with Moq for mocking
- **New tests required**: YES (for proxy integration, SessionFactory, DotnetSpider entities)

### TDD Structure
Each TODO follows RED-GREEN-REFACTOR:
1. **RED**: Write failing test
2. **GREEN**: Implement minimum code to pass
3. **REFACTOR**: Clean up while keeping tests green

### Test Categories
1. **Unit Tests**: Individual components (parsers, retry logic, DotnetSpider entities)
2. **Integration Tests**: Full scraping workflows with DotnetSpider
3. **Anti-Bot Tests**: Proxy rotation, header consistency, DotnetSpider scheduling
4. **DotnetSpider Tests**: Entity model validation, selector extraction, data flow

---

## Execution Strategy

### Parallel Execution Waves (Updated)

```
Wave 1 (Foundation - Week 1-2):
├── Task 1: Enhance Ghost browser stealth capabilities
├── Task 2: Integrate SessionFactory into Indeed
├── Task 3: Integrate SessionFactory into Glassdoor
└── Task 4: Integrate SessionFactory into Google Jobs

Wave 1.5 (DotnetSpider Integration - Week 2-3):
├── Task 2.5: Create DotnetSpider integration layer
├── Task 2.6: Define entity models for all platforms
├── Task 2.7: Implement selector-based parsers
└── Task 2.8: Integrate DotnetSpider statistics

Wave 2 (Proxy System - Week 2-3):
├── Task 5: Implement abstract proxy configuration system
├── Task 6: Create proxy health checking and rotation
├── Task 7: Add geographic targeting support
└── Task 8: Integrate with existing proxy sources

Wave 3 (Parsing - Week 3-4):
├── Task 9: Implement multi-strategy parsers for Indeed
├── Task 10: Implement multi-strategy parsers for Glassdoor
├── Task 11: Implement multi-strategy parsers for Google Jobs
└── Task 12: Add structured logging

Wave 4 (Integration - Week 4-5):
├── Task 13: Implement session token synchronization
├── Task 14: Add free third-party API fallbacks
├── Task 15: Implement circuit breaker patterns
└── Task 16: Add monitoring and alerting

Wave 5 (Testing & Deployment - Week 5-6):
├── Task 17: Comprehensive integration testing
├── Task 18: Performance benchmarking
├── Task 19: Documentation and runbooks
└── Task 20: Production deployment with canary
```

### Dependency Matrix (Updated)

| Task | Depends On | Blocks | Can Parallelize With |
|------|------------|--------|---------------------|
| 1 (Stealth) | None | 2-4 | None |
| 2-4 (Sessions) | 1 | 9-11 (Parsers) | Each other |
| 2.5-2.8 (DotnetSpider) | 1 | 9-11 (Enhanced parsers) | 5-8 (proxies) |
| 5-8 (Proxy) | None | 13, 14 | 9-12 (parsers) |
| 9-11 (Parsers) | 2-4, 2.5-2.8 | 17 | Each other |
| 13-16 (Integration) | 2-4, 5-8, 9-12 | 17 | Each other |
| 17 (Tests) | 9-16 | 20 | None |
| 20 (Deployment) | 17 | None | None |

---

## TODOs

### Phase 1: Foundation (Week 1-2)

#### Task 1: Enhance Ghost Browser Stealth Capabilities
*(Same as original plan)*

#### Task 2: Integrate SessionFactory into Indeed
*(Same as original plan)*

#### Task 3: Integrate SessionFactory into Glassdoor
*(Same as original plan)*

#### Task 4: Integrate SessionFactory into Google Jobs
*(Same as original plan)*

---

### Phase 1.5: DotnetSpider Integration (Week 2-3)

#### Task 2.5: Create DotnetSpider Integration Layer

**What to do**:
- [ ] Create `DotnetSpiderSpiderAdapter` class to wrap DotnetSpider
- [ ] Implement custom downloader to use SessionFactory sessions
- [ ] Create response conversion from DotnetSpider Response to Ghost models
- [ ] Add configuration options for DotnetSpider features
- [ ] Implement statistics integration with Ghost monitoring

**Must NOT do**:
- Do NOT replace existing downloaders - use them as data sources
- Do NOT use DotnetSpider's message queue (keep local)
- Do NOT change DotnetSpider core library

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`
- **Reason**: Adapter pattern implementation with careful integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-8)
- **Parallel Group**: Wave 1.5
- **Blocks**: Tasks 2.6-2.8
- **Blocked By**: Task 1

**References**:
- DotnetSpider: `/DotnetSpider/` - Your fork
- Spider Base: `/DotnetSpider/src/DotnetSpider/Spider.cs` - Core Spider class
- Builder: `/DotnetSpider/src/DotnetSpider/Builder.cs` - Builder pattern
- Downloader: `/DotnetSpider/src/DotnetSpider/Downloader/IDownloader.cs` - Downloader interface

**Acceptance Criteria**:
- [ ] Adapter class successfully wraps DotnetSpider
- [ ] Custom downloader uses SessionFactory
- [ ] Response conversion works
- [ ] Configuration options available
- [ ] Statistics integrated

**Test Plan**:
```csharp
[Fact]
public async Task DotnetSpiderAdapter_ParsesHtmlResponse()
{
    var adapter = new DotnetSpiderSpiderAdapter(sessionFactory, options, logger);
    var html = await GetSampleIndeedJobsHtml();
    var jobs = await adapter.ParseAsync("indeed", html);
    Assert.NotNull(jobs);
    Assert.NotEmpty(jobs);
}
```

**Commit**: YES
- Message: `feat(dotnetspider): create integration adapter layer`
- Files: `src/Platforms/Ghost.Platform.Common/DotnetSpider/`

---

#### Task 2.6: Define Entity Models for All Platforms

**What to do**:
- [ ] Create `IndeedJobEntity` with DotnetSpider entity attributes
- [ ] Create `GlassdoorJobEntity` with entity attributes
- [ ] Create `GoogleJobsEntity` with entity attributes
- [ ] Define selector expressions for each field using XPath/CSS
- [ ] Add formatters for data transformation (trim, replace, date parsing)
- [ ] Define pagination selectors for each platform

**Must NOT do**:
- Do NOT use existing parser logic - use selectors only
- Do NOT duplicate existing job models - extend or adapt them
- Do NOT create overly complex selector expressions

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: Entity model definition

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-8)
- **Parallel Group**: Wave 1.5
- **Blocks**: Task 2.7
- **Blocked By**: Task 2.5

**References**:
- Entity Example: `/DotnetSpider/src/DotnetSpider.Sample/samples/EntitySpider.cs` - Example entity
- Entity Base: `/DotnetSpider/src/DotnetSpider/DataFlow/Parser/Model.cs` - EntityBase class
- Attributes: `/DotnetSpider/src/DotnetSpider/DataFlow/Parser/` - Selector attributes

**Acceptance Criteria**:
- [ ] All three entity models defined
- [ ] Selector expressions work with sample HTML
- [ ] Formatters apply correctly
- [ ] Pagination selectors defined
- [ ] Unit tests pass

**Commit**: YES
- Message: `feat(dotnetspider): define entity models for job platforms`
- Files: `src/Platforms/Ghost.Platform.Common/DotnetSpider/Entities/`

---

#### Task 2.7: Implement Selector-Based Parsers

**What to do**:
- [ ] Implement parser to convert HTML to DotnetSpider entity format
- [ ] Add support for multi-page parsing using FollowRequestSelector
- [ ] Implement error handling for missing selectors
- [ ] Add logging for selected data
- [ ] Create fallback to original parsers if DotnetSpider fails

**Must NOT do**:
- Do NOT break backwards compatibility
- Do NOT require complete HTML restructure - allow partial success
- Do NOT ignore parsing errors without logging

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`
- **Reason**: Parser integration with fallback handling

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-8)
- **Parallel Group**: Wave 1.5
- **Blocks**: Task 2.8
- **Blocked By**: Tasks 2.5, 2.6

**References**:
- DataParser: `/DotnetSpider/src/DotnetSpider/DataFlow/Parser/DataParser.cs` - DotnetSpider parser
- Selector System: `/DotnetSpider/src/DotnetSpider/DataFlow/Parser/Selector.cs` - Selector interface
- Current Parsers: Existing parser files for each platform

**Acceptance Criteria**:
- [ ] HTML conversion works
- [ ] Multi-page parsing functional
- [ ] Error handling implemented
- [ ] Logging captures parsing results
- [ ] Fallback to original parsers works

**Commit**: YES
- Message: `feat(dotnetspider): implement selector-based parsers`
- Files: `src/Platforms/Ghost.Platform.Common/DotnetSpider/Parsers/`

---

#### Task 2.8: Integrate DotnetSpider Statistics

**What to do**:
- [ ] Create custom statistics store for Ghost monitoring
- [ ] Map DotnetSpider statistics to Ghost metrics
- [ ] Implement statistics aggregation per platform
- [ ] Add health check based on DotnetSpider statistics
- [ ] Create dashboard for DotnetSpider-specific metrics

**Must NOT do**:
- Do NOT lose existing Ghost statistics
- Do NOT duplicate metrics - aggregate instead
- Do NOT break existing monitoring

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Statistics integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-8, 9-12)
- **Parallel Group**: Wave 1.5
- **Blocks**: Task 16 (Monitoring)
- **Blocked By**: Task 2.5

**References**:
- Statistics Service: `/DotnetSpider/src/DotnetSpider/Statistic/` - Statistics interfaces
- InMemory Store: `/DotnetSpider/src/DotnetSpider/Statistic/Store/` - Store implementations
- Current Monitoring: Existing Ghost monitoring infrastructure

**Acceptance Criteria**:
- [ ] Custom statistics store created
- [ ] Statistics mapped to Ghost metrics
- [ ] Aggregation works per platform
- [ ] Health check functional
- [ ] Dashboard displays DotnetSpider metrics

**Commit**: YES
- Message: `feat(dotnetspider): integrate statistics with Ghost monitoring`
- Files: `src/Platforms/Ghost.Platform.Common/Monitoring/DotnetSpiderStatistics.cs`

---

### Phase 2: Proxy System (Week 2-3)

#### Task 5: Implement Abstract Proxy Configuration System
*(Same as original plan)*

#### Task 6: Create Proxy Health Checking and Rotation
*(Same as original plan)*

#### Task 7: Add Geographic Targeting Support
*(Same as original plan)*

#### Task 8: Integrate with Existing Proxy Sources
*(Same as original plan)*

---

### Phase 3: Parsing (Week 3-4)

#### Task 9: Implement Multi-Strategy Parser for Indeed

**(Updated with DotnetSpider integration)**

**What to do**:
- [ ] Refactor IndeedJobParser to support multiple strategies
- [ ] **Primary strategy**: Use DotnetSpider entity parser
- [ ] **Fallback 1**: Original JSON GraphQL parsing
- [ ] **Fallback 2**: Heuristic regex-based parsing
- [ ] Add structured logging for parsing decisions
- [ ] Capture raw samples on parse failures

**Must NOT do**:
- Do NOT rely solely on DotnetSpider - keep fallbacks
- Do NOT break existing parser behavior

**Acceptance Criteria**:
- [ ] DotnetSpider entity parser used as primary
- [ ] Fallback strategies work
- [ ] Structured logging captures path
- [ ] All existing tests pass

**Commit**: 
- Message: `feat(indeed): implement multi-strategy parser with DotnetSpider`
- Files: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`

---

#### Task 10: Implement Multi-Strategy Parser for Glassdoor
**(Updated with DotnetSpider integration - similar to Task 9)**

#### Task 11: Implement Multi-Strategy Parser for Google Jobs
**(Updated with DotnetSpider integration - similar to Task 9)**

#### Task 12: Add Structured Logging
*(Same as original plan)*

---

### Phase 4: Integration (Week 4-5)

#### Task 13: Implement Session Token Synchronization
*(Same as original plan)*

#### Task 14: Add Free Third-Party API Fallbacks
*(Same as original plan)*

#### Task 15: Implement Circuit Breaker Patterns
*(Same as original plan)*

#### Task 16: Add Monitoring and Alerting
*(Same as original plan, enhanced with DotnetSpider statistics from Task 2.8)*

---

### Phase 5: Testing & Deployment (Week 5-6)

#### Task 17: Comprehensive Integration Testing
**(Updated with DotnetSpider tests)**

**What to do**:
- [ ] Tests for all platforms with SessionFactory
- [ ] Tests for proxy rotation
- [ ] **DotnetSpider entity parser tests**
- [ ] Tests for DotnetSpider fallback strategies
- [ ] Tests for consent page handling
- [ ] Tests for third-party API fallbacks
- [ ] Performance benchmarks

**Acceptance Criteria**:
- [ ] >90% code coverage
- [ ] DotnetSpider parser tests pass
- [ ] Fallback strategy tests pass
- [ ] All integration tests pass

---

#### Task 18: Performance Benchmarking
*(Same as original plan)*

#### Task 19: Documentation and Runbooks
*(Same as original plan, add DotnetSpider documentation)*

#### Task 20: Production Deployment with Canary
*(Same as original plan)*

---

## DotnetSpider Entity Examples

### Indeed Job Entity Example

```csharp
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Formatters;
using DotnetSpider.Selector;

namespace Ghost.Platform.Indeed.DotnetSpider;

[Schema("ghost", "indeed_jobs")]
[EntitySelector(Expression = "//div[contains(@class, 'jobsearch-ResultsList')]/div[contains(@class, 'job_')]")]
public class IndeedJobEntity : EntityBase<IndeedJobEntity>
{
    protected override void Configure()
    {
        HasIndex(x => x.Title);
        HasIndex(x => new { x.Employer, x.Location }, false);
    }

    [ValueSelector(Expression = ".//span[@class='jobTitle']", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string Title { get; set; }

    [ValueSelector(Expression = ".//span[@class='companyName']", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string Employer { get; set; }

    [ValueSelector(Expression = ".//div[@class='companyLocation']", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string Location { get; set; }

    [ValueSelector(Expression = ".//div[contains(@class, 'job-snippet')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string Description { get; set; }

    [ValueSelector(Expression = ".//span[@class='salary-snippet']/text()", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string Salary { get; set; }

    [ValueSelector(Expression = ".//a[contains(@class, 'jcs-JobTitle')]/@href", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string JobUrl { get; set; }

    // Pagination
    [FollowRequestSelector(Expression = "//a[contains(@class, 'pn')]")]
    public string NextPageUrl { get; set; }
}
```

### Configuration Example

```json
{
  "JobScrapers": {
    "UseDotnetSpider": true,
    "DotnetSpider": {
      "Enable": true,
      "Speed": 1.0,
      "RetriedTimes": 3,
      "Batch": 1,
      "Depth": 5
    },
    "Indeed": {
      "Enabled": true,
      "ApiKey": "...",
      "Strategy": "HttpFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 1000,
      "DelayMaxMs": 3000,
      "MaxRetries": 3
    },
    "Glassdoor": {
      "Enabled": true,
      "Strategy": "BrowserFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 2000,
      "DelayMaxMs": 5000,
      "MaxRetries": 4
    },
    "GoogleJobs": {
      "Enabled": true,
      "Strategy": "BrowserFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 500,
      "DelayMaxMs": 1500,
      "MaxRetries": 3
    },
    "Proxies": {
      "Sources": [
        {
          "Type": "Static",
          "Enabled": true,
          "Username": "...",
          "Password": "...",
          "Hosts": ["proxy1:port", "proxy2:port"]
        },
        {
          "Type": "Api",
          "Enabled": true,
          "Url": "http://proxy-api-endpoint.com/proxies"
        }
      ],
      "RotationStrategy": "RoundRobin",
      "HealthCheckIntervalMinutes": 5
    }
  }
}
```

---

## Success Criteria (Updated with DotnetSpider)

### Verification Commands

```bash
# Run all tests
dotnet test tests/Ghost.Platform.Indeed.Tests
dotnet test tests/Ghost.Platform.Glassdoor.Tests
dotnet test tests/Ghost.Platform.Google.Tests
dotnet test tests/Platforms.Ghost.Platform.Common.DotnetSpider.Tests

# Run integration tests with DotnetSpider
dotnet test tests/Integration.Tests --filter "DotnetSpider"

# Check code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run benchmarks
dotnet run --project benchmarks/Ghost.Benchmarks

# Verify metrics endpoint
curl http://localhost:5000/metrics
```

### Final Checklist (Updated)
- [ ] All TODOs complete
- [ ] All tests passing (including DotnetSpider tests)
- [ ] Code coverage >90%
- [ ] Documentation complete (including DotnetSpider integration)
- [ ] Production deployed
- [ ] Success rate >95% (measured over 7 days)
- [ ] Error rate <1%
- [ ] Consent handling >90% success
- [ ] DotnetSpider entity parsers functional
- [ ] Monitoring operational with DotnetSpider metrics
- [ ] Runbooks tested

---

## Notes

### Anti-Patterns Avoided
*(Same as original plan)*

### Performance Considerations (Updated)
- Connection pooling via SessionFactory sessions
- **DotnetSpider request scheduling with FixedTokenBucket rate limiting**
- Async/await throughout
- **DotnetSpider data flow pipeline for efficient processing**
- Minimal allocations in hot paths
- Caching of session tokens

### Legal/Ethical Considerations
*(Same as original plan)*

---

**Ready to execute**: Run `/start-work` to begin implementation

---

## Appendix: DotnetSpider Integration Benefits

### What DotnetSpider Brings

1. **Declarative Data Extraction**
   - No more brittle JSON property paths
   - Visual selector definitions (CSS/XPath)
   - Built-in formatters (trim, replace, regex, date parsing)

2. **Request Lifecycle Management**
   - Automatic retry with configurable limits
   - Request timeout handling
   - Depth-limited crawling
   - Request deduplication

3. **Statistics & Monitoring**
   - Success/failure tracking per agent
   - Request queue statistics
   - Performance metrics
   - Spider health indicators

4. **Extensibility**
   - Custom downloaders (we could add a GhostBrowserDownloader)
   - Custom formatters
   - Custom storage backends
   - Multi-platform support

### Implementation Notes

- **Non-intrusive**: DotnetSpider operates on HTML responses; doesn't replace our Ghost browser infrastructure
- **Enhancement, not replacement**: Adds sophisticated parsing capabilities while keeping existing downloaders, proxies, and stealth
- **Incremental adoption**: Can start with one platform and migrate others gradually
- **Fallback assurance**: If DotnetSpider parsing fails, we fall back to original parsers

---

**Key Decision**: We're using DotnetSpider as a **data extraction and processing engine**, not replacing:
- Ghost browser (stealth, automation)
- SessionFactory (session management)
- Proxy system (rotation, health checks)
- Existing downloaders

This creates the best of both worlds: Ghost's excellent browser automation with DotnetSpider's sophisticated data extraction.
