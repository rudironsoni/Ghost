# Job Scraper Reliability Enhancement - Revised Work Plan

## TL;DR

**Objective**: Fix broken job scrapers for Indeed, Glassdoor, and Google Jobs to achieve near 100% reliability

**Deliverables**:
- Leverage existing Ghost browser stealth capabilities for anti-bot protection
- Unified session management using RotatingProxySession across all platforms
- Robust parsing engines with fallback strategies
- Intelligent error handling with consent/CAPTCHA management
- Abstract proxy system supporting any residential proxy provider
- Free third-party API fallbacks for ULTRA MISER MODE ($0)

**Estimated Effort**: Large (5-6 weeks)
**Parallel Execution**: YES - Platform teams can work in parallel
**Critical Path**: Shared infrastructure → Platform-specific implementations → Integration testing

---

## Context

### Original Request
Deep investigation into broken job scrapers that return zero, null, or empty results. Need root cause analysis and solutions for:
- Indeed (@src/Platforms/Ghost.Platform.Indeed/)
- Glassdoor (@src/Platforms/Ghost.Platform.Glassdoor/)
- Google Jobs (@src/Platforms/Ghost.Platform.Google/Jobs/)

### Key Findings from Analysis

**Root Causes Identified**:
1. **Indeed**: Per-request HttpClient creation breaks session continuity; static headers easily fingerprinted; rigid GraphQL parsing
2. **Glassdoor**: Hardcoded fallback CSRF token may be invalid; duplicate CSRF extraction logic; limited consent handling
3. **Google Jobs**: Manual cookie header injection; unused CookieContainer; inconsistent UA/Sec-CH headers; brittle parser with fixed index positions

**Common Issues Across All Platforms**:
- Inadequate anti-bot protections (no residential proxy rotation)
- Poor session management (no connection pooling)
- Brittle parsing logic (no fallback strategies)
- Insufficient error handling (hardcoded tokens, silent failures)

### Architecture Decisions
- Use existing Ghost browser stealth capabilities instead of implementing new ones
- Use existing RotatingProxySession infrastructure (currently unused by scrapers)
- Implement multi-strategy parsing with fallback
- Create abstract proxy system that works with any provider
- Use only free third-party APIs for fallbacks (ULTRA MISER MODE)
- Maintain backward compatibility with existing options

---

## Work Objectives

### Core Objective
Transform unreliable job scrapers into production-grade, near-100% reliable data collection systems through systematic improvements to anti-bot evasion, session management, and error recovery.

### Concrete Deliverables
1. **Enhanced Session Management Framework**
   - All platforms use RotatingProxySession
   - Persistent CookieContainer handling
   - Connection pooling and reuse

2. **Abstract Proxy System**
   - Support for any residential proxy provider through configuration
   - Proxy rotation with health checks
   - Geographic targeting support

3. **Robust Parsing Engine**
   - Multi-strategy parsing (structured → DOM → heuristic → regex)
   - Schema evolution handling
   - Structured logging of parsing decisions

4. **Intelligent Error Recovery**
   - Advanced consent page handling
   - Circuit breaker patterns
   - Browser escalation paths
   - Free third-party API fallbacks

5. **Monitoring & Observability**
   - Metrics collection (success rate, errors, latency)
   - Health checks and alerting
   - Debug logging with request/response capture

### Definition of Done
- [ ] All three platforms achieve >95% success rate in integration tests
- [ ] <1% error rate from anti-bot detection (blocking, rate limiting)
- [ ] Consent page handling succeeds >90% of the time
- [ ] All scrapers use unified RotatingProxySession infrastructure
- [ ] Comprehensive test coverage for new features
- [ ] Monitoring dashboard operational

### Must Have
- Abstract proxy integration supporting any provider
- Proper cookie handling with CookieContainer
- Multi-strategy parsers with fallback
- Consent page detection and handling
- Free third-party API fallbacks
- Structured error reporting

### Must NOT Have (Guardrails)
- No breaking changes to existing public APIs
- No hardcoded tokens or credentials in code
- No reliance on single proxy provider
- No exceptions that crash the application
- No excessive resource consumption (memory leaks)
- No paid third-party services

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: YES (test projects exist for all platforms)
- **User wants tests**: TDD (Write tests first, then implementation)
- **Framework**: xUnit with Moq for mocking
- **New tests required**: YES (for proxy integration, consent handling, multi-strategy parsers)

### TDD Structure
Each TODO follows RED-GREEN-REFACTOR:
1. **RED**: Write failing test
2. **GREEN**: Implement minimum code to pass
3. **REFACTOR**: Clean up while keeping tests green

### Test Categories
1. **Unit Tests**: Individual components (parsers, retry logic)
2. **Integration Tests**: Full scraping workflows
3. **Anti-Bot Tests**: Proxy rotation, header consistency
4. **Error Recovery Tests**: Consent handling, fallback strategies

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation - Week 1-2):
├── Task 1: Enhance Ghost browser stealth capabilities
├── Task 2: Integrate RotatingProxySession into Indeed
├── Task 3: Integrate RotatingProxySession into Glassdoor
├── Task 4: Integrate RotatingProxySession into Google Jobs

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

Critical Path: Task 1 → Task 5 → Task 13 → Task 17 → Task 20
Parallel Speedup: ~40% faster than sequential
```

### Dependency Matrix

| Task | Depends On | Blocks | Can Parallelize With |
|------|------------|--------|---------------------|
| 1 (Stealth) | None | 2-4 | None |
| 2-4 (Sessions) | 1 | 9-11 (Parsers) | Each other (platform-specific) |
| 5-8 (Proxy) | None | 13, 14 | Each other |
| 9-11 (Parsers) | 2-4 | 17 | Each other |
| 13 (Token Sync) | 2-4, 5-8 | 17 | 9-12 |
| 17 (Integration Tests) | 9-13 | 20 | None |
| 20 (Deployment) | 17 | None | None |

---

## TODOs

### Phase 1: Foundation (Week 1-2)

#### Task 1: Enhance Ghost Browser Stealth Capabilities

**What to do**:
- [ ] Analyze current Ghost stealth implementation
- [ ] Add missing stealth techniques (canvas noise, audio context spoofing)
- [ ] Implement more realistic fingerprint profiles
- [ ] Add support for custom stealth scripts
- [ ] Improve WebGL spoofing for modern GPUs

**Must NOT do**:
- Do NOT break existing stealth functionality
- Do NOT add performance-heavy stealth techniques
- Do NOT make stealth detection worse

**Recommended Agent Profile**:
- **Category**: `artistry` (creative solutions needed)
- **Skills**: `playwright`, `git-master`
- **Reason**: Requires creative bypass techniques using existing Ghost infrastructure

**Parallelization**:
- **Can Run In Parallel**: YES
- **Parallel Group**: Wave 1
- **Blocks**: Tasks 2-4
- **Blocked By**: None

**References**:
- Current Implementation: `/src/Core/Ghost/Stealth/StealthScripts.cs` - Existing stealth scripts
- Fingerprint Generator: `/src/Core/Ghost/Stealth/FingerprintGenerator.cs` - Profile generation
- Ghost Kernel: `/src/Core/Ghost/Core/GhostKernel.cs` - Session creation with stealth

**Acceptance Criteria**:
- [ ] Enhanced stealth scripts implemented
- [ ] More realistic fingerprint profiles
- [ ] Custom stealth script support
- [ ] Existing tests pass
- [ ] Browser detection tests pass

**Test Plan**:
```csharp
// Test: Enhanced stealth capabilities
[Fact]
public async Task NewSession_HasEnhancedStealth()
{
    var options = new KernelOptions { EnableStealth = true };
    using var kernel = await GhostKernel.CreateAsync(options);
    using var session = await kernel.NewSessionAsync();
    var page = await session.Page.GotoAsync("https://bot.sannysoft.com");
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    // Verify stealth indicators pass
}
```

**Commit**: YES - Individual commit after stealth enhancements
- Message: `feat(ghost): enhance stealth capabilities for better bot detection avoidance`
- Files: `src/Core/Ghost/Stealth/*.cs`
- Pre-commit: `dotnet test tests/Core/Ghost.Tests/Stealth`

---

#### Task 2: Integrate RotatingProxySession into Indeed

**What to do**:
- [ ] Modify `IndeedApiClient` constructor to accept `RotatingProxySession` or `SessionFactory` via DI
- [ ] Replace per-request HttpClient creation with `session.ExecuteAsync(() => request)`
- [ ] Remove manual cookie header injection, use CookieContainer
- [ ] Wire `IndeedOptions.DelayMinMs`/`DelayMaxMs` into rate limiting
- [ ] Add User-Agent rotation from pool

**Must NOT do**:
- Do NOT remove existing constructor (maintain backward compatibility)
- Do NOT change public API surface of `IndeedJobClient`
- Do NOT hardcode proxy credentials

**Recommended Agent Profile**:
- **Category**: `unspecified-high` (requires careful refactoring)
- **Skills**: `git-master`, `frontend-ui-ux` (for testing)
- **Reason**: Need to modify existing HTTP client patterns while maintaining compatibility

**Parallelization**:
- **Can Run In Parallel**: YES
- **Parallel Group**: Wave 1
- **Blocks**: Task 9 (Indeed Parser)
- **Blocked By**: Task 1

**References**:
- Pattern: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs:15-45` - Session management with proxy rotation
- Pattern: `/src/Platforms/Ghost.Platform.Common/Session/SessionFactory.cs:20-50` - Platform-specific session creation
- API: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs:76-142` - Current HttpClient creation
- Options: `/src/Platforms/Ghost.Platform.Indeed/IndeedOptions.cs:30-50` - Delay settings (currently unused)

**Acceptance Criteria**:
- [ ] `IndeedApiClient` can be constructed with `RotatingProxySession`
- [ ] All HTTP requests go through session (verified via mock)
- [ ] Cookie persistence works across pagination (verified in test)
- [ ] Delay settings from options are applied with jitter
- [ ] Existing tests pass without modification

**Test Plan**:
```csharp
// Test: RotatingProxySession integration
[Fact]
public async Task SearchAsync_UsesRotatingProxySession()
{
    var mockSession = new Mock<IRotatingProxySession>();
    var client = new IndeedApiClient(mockSession.Object, options, logger);
    await client.SearchAsync("developer", "NYC", 10);
    mockSession.Verify(s => s.ExecuteAsync(It.IsAny<Func<HttpRequestMessage>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
}
```

**Commit**: YES - Individual commit after each platform
- Message: `feat(indeed): integrate RotatingProxySession for session management`
- Files: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs`
- Pre-commit: `dotnet test tests/Ghost.Platform.Indeed.Tests`

---

#### Task 3: Integrate RotatingProxySession into Glassdoor

**What to do**:
- [ ] Modify `GlassdoorApiClient` to use `RotatingProxySession`
- [ ] Update `GlassdoorBrowserClient` to use session factory for proxy selection
- [ ] Unify CSRF token extraction between API and browser clients
- [ ] Remove hardcoded fallback token usage
- [ ] Add proper cookie persistence

**Must NOT do**:
- Do NOT delete the fallback token constant yet (mark as obsolete)
- Do NOT change `GlassdoorJobClient` public API
- Do NOT break existing browser fallback logic

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Need to coordinate changes between API and browser clients

**Parallelization**:
- **Can Run In Parallel**: YES (with Task 2)
- **Parallel Group**: Wave 1
- **Blocks**: Task 10 (Glassdoor Parser)
- **Blocked By**: Task 1

**References**:
- Shared: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs` - Session management
- API Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs:60-95` - CSRF extraction
- Browser Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorBrowserClient.cs:194-206` - Session creation
- Constants: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorConstants.cs:113` - Fallback token

**Acceptance Criteria**:
- [ ] `GlassdoorApiClient` uses RotatingProxySession
- [ ] CSRF token extraction unified (single source of truth)
- [ ] Hardcoded fallback token marked obsolete with warning
- [ ] Cookie container persists across API and browser
- [ ] Existing tests pass

**Commit**:
- Message: `feat(glassdoor): integrate RotatingProxySession and unify CSRF extraction`
- Files: `src/Platforms/Ghost.Platform.Glassdoor/Internal/*.cs`

---

#### Task 4: Integrate RotatingProxySession into Google Jobs

**What to do**:
- [ ] Modify `GoogleJobsApiClient` to accept RotatingProxySession
- [ ] Remove manual Cookie header injection
- [ ] Ensure User-Agent consistency with Sec-CH-UA headers
- [ ] Add browser escalation path when consent detected
- [ ] Fix async pagination to use session

**Must NOT do**:
- Do NOT remove async pagination logic
- Do NOT change default strategy (BrowserFirst)
- Do NOT break existing consent detection

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Complex coordination between API and browser clients

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 2-3)
- **Parallel Group**: Wave 1
- **Blocks**: Task 11 (Google Parser)
- **Blocked By**: Task 1

**References**:
- API Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs:16` - Unused CookieContainer
- Cookie Issue: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs:111-112` - Manual cookie header
- Headers: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs:48-77` - Search headers with static UA
- Browser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs:145-159` - Consent handling

**Acceptance Criteria**:
- [ ] CookieContainer used instead of manual headers
- [ ] User-Agent rotation updates all Sec-CH headers
- [ ] Browser escalation works when consent detected
- [ ] Async pagination uses session
- [ ] Existing tests pass

**Commit**:
- Message: `feat(google): integrate RotatingProxySession and fix cookie handling`
- Files: `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`

---

### Phase 2: Proxy System (Week 2-3)

#### Task 5: Implement Abstract Proxy Configuration System

**What to do**:
- [ ] Create configuration schema for abstract proxy providers
- [ ] Implement proxy source factory for different provider types
- [ ] Add support for multiple proxy sources simultaneously
- [ ] Create proxy configuration validation
- [ ] Document configuration examples

**Must NOT do**:
- Do NOT couple to specific proxy providers
- Do NOT hardcode provider-specific logic
- Do NOT require all proxy types to be configured

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Configuration system implementation

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 6-8)
- **Parallel Group**: Wave 2
- **Blocks**: Task 13 (Token Sync), Task 17 (Integration Tests)
- **Blocked By**: None

**References**:
- Existing: `/src/Core/Ghost/Core/ProxyOptions.cs` - Current proxy options
- Sources: `/src/Core/Ghost/Services/StaticProxySource.cs` - Static proxy source
- Provider: `/src/Core/Ghost/Abstractions/IProxyProvider.cs` - Provider interface

**Acceptance Criteria**:
- [ ] Configuration schema supports multiple proxy types
- [ ] Proxy source factory implemented
- [ ] Multiple sources can be configured
- [ ] Configuration validation works
- [ ] Examples documented

**Commit**:
- Message: `feat(common): implement abstract proxy configuration system`
- Files: `src/Core/Ghost/ProxyConfiguration/`

---

#### Task 6: Create Proxy Health Checking and Rotation

**What to do**:
- [ ] Implement proxy health checking mechanism
- [ ] Add proxy performance metrics collection
- [ ] Create proxy rotation strategies (round-robin, performance-based)
- [ ] Add proxy fallback when primary fails
- [ ] Implement proxy blacklist/whitelist

**Must NOT do**:
- Do NOT use blocking health checks
- Do NOT rotate proxies mid-request
- Do NOT ignore proxy performance data

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`
- **Reason**: Complex proxy management logic

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5, 7-8)
- **Parallel Group**: Wave 2
- **Blocks**: Task 13, Task 17
- **Blocked By**: Task 5

**References**:
- Session: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs:139-171` - Proxy refresh
- Provider: `/src/Core/Ghost/Services/RotatingProxyProvider.cs` - Current rotation logic

**Acceptance Criteria**:
- [ ] Health checking mechanism implemented
- [ ] Performance metrics collected
- [ ] Rotation strategies available
- [ ] Fallback mechanism works
- [ ] Blacklist/whitelist supported

**Commit**:
- Message: `feat(common): add proxy health checking and rotation strategies`
- Files: `src/Core/Ghost/ProxyManagement/`

---

#### Task 7: Add Geographic Targeting Support

**What to do**:
- [ ] Implement geographic proxy selection
- [ ] Add country/region mapping for proxies
- [ ] Create location-aware proxy pools
- [ ] Add geographic targeting configuration
- [ ] Implement proxy location validation

**Must NOT do**:
- Do NOT assume all proxies have geographic metadata
- Do NOT ignore proxy location accuracy
- Do NOT use inaccurate location data

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Geographic targeting implementation

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-6, 8)
- **Parallel Group**: Wave 2
- **Blocks**: Task 17
- **Blocked By**: Task 5

**References**:
- Options: `/src/Core/Ghost/Core/ProxyOptions.cs` - Current proxy options
- Session: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs:160` - Country code usage

**Acceptance Criteria**:
- [ ] Geographic selection implemented
- [ ] Country/region mapping available
- [ ] Location-aware pools created
- [ ] Configuration options added
- [ ] Location validation works

**Commit**:
- Message: `feat(common): add geographic targeting support for proxies`
- Files: `src/Core/Ghost/GeoTargeting/`

---

#### Task 8: Integrate with Existing Proxy Sources

**What to do**:
- [ ] Connect abstract proxy system with existing sources
- [ ] Add support for static proxy lists
- [ ] Add support for API-based proxy sources
- [ ] Implement proxy source compatibility layer
- [ ] Add proxy source health monitoring

**Must NOT do**:
- Do NOT break existing proxy source functionality
- Do NOT duplicate existing proxy source logic
- Do NOT ignore proxy source errors

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Integration with existing components

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-7)
- **Parallel Group**: Wave 2
- **Blocks**: Task 17
- **Blocked By**: Tasks 5-7

**References**:
- Static: `/src/Core/Ghost/Services/StaticProxySource.cs` - Static proxy source
- API: `/src/Core/Ghost/Services/ApiProxySource.cs` - API proxy source
- Source: `/src/Core/Ghost/Abstractions/IProxySource.cs` - Source interface

**Acceptance Criteria**:
- [ ] Abstract system integrated with existing sources
- [ ] Static proxy lists supported
- [ ] API-based sources supported
- [ ] Compatibility layer implemented
- [ ] Health monitoring added

**Commit**:
- Message: `feat(common): integrate abstract proxy system with existing sources`
- Files: `src/Core/Ghost/ProxyIntegration/`

---

### Phase 3: Parsing (Week 3-4)

#### Task 9: Implement Multi-Strategy Parser for Indeed

**What to do**:
- [ ] Refactor IndeedJobParser to support multiple strategies
- [ ] Add fallback property path lookups
- [ ] Implement schema evolution handling
- [ ] Add structured logging for parsing decisions
- [ ] Capture raw JSON samples on parse failures

**Must NOT do**:
- Do NOT break existing parser behavior (backward compatible)
- Do NOT use reflection for property access (performance)
- Do NOT silently skip parse errors

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: Refactoring existing parser

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 10-11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17 (Integration Tests)
- **Blocked By**: Task 2

**References**:
- Current Parser: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`
- Tests: `/tests/Ghost.Platform.Indeed.Tests/IndeedJobParserTests.cs`
- JSON: Sample Indeed responses from logs

**Acceptance Criteria**:
- [ ] Multiple parsing strategies implemented
- [ ] Fallback paths work for changed schemas
- [ ] Structured logging captures parsing path
- [ ] Raw samples captured on failures
- [ ] All existing tests pass

**Commit**:
- Message: `feat(indeed): implement multi-strategy parser with fallback`
- Files: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`

---

#### Task 10: Implement Multi-Strategy Parser for Glassdoor

**What to do**:
- [ ] Refactor GlassdoorJobParser with multiple strategies
- [ ] Add recursive JSON scanning with fallback paths
- [ ] Implement heuristic job array detection
- [ ] Add structured logging
- [ ] Capture response samples

**Must NOT do**:
- Do NOT rely solely on property name heuristics
- Do NOT silently discard parse exceptions
- Do NOT break existing tests

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: Similar to Task 9

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9, 11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: Task 3

**References**:
- Parser: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`
- Tests: `/tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorJobParserTests.cs`

**Acceptance Criteria**:
- [ ] Multi-strategy parser implemented
- [ ] Heuristic detection works for job arrays
- [ ] Structured logging added
- [ ] Samples captured on failures

**Commit**:
- Message: `feat(glassdoor): implement multi-strategy parser with fallback`
- Files: `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`

---

#### Task 11: Implement Multi-Strategy Parser for Google Jobs

**What to do**:
- [ ] Refactor GoogleJobsParser with DOM-based primary strategy
- [ ] Add AngleSharp integration for HTML parsing
- [ ] Keep legacy strategies as fallbacks
- [ ] Add structured logging
- [ ] Remove fixed index positions (brittle)

**Must NOT do**:
- Do NOT remove widget key parsing (keep as fallback)
- Do NOT break existing parser tests
- Do NOT use regex for primary parsing

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: DOM parsing requires HTML parser integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9-10)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: Task 4

**References**:
- Parser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`
- AngleSharp: NuGet package for HTML parsing
- Tests: `/tests/Ghost.Platform.Google.Tests/GoogleJobsParserTests.cs`

**Acceptance Criteria**:
- [ ] AngleSharp-based parsing implemented
- [ ] Legacy strategies preserved as fallbacks
- [ ] Fixed index positions removed
- [ ] Structured logging added
- [ ] All parser tests pass

**Commit**:
- Message: `feat(google): implement DOM-based multi-strategy parser`
- Files: `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`

---

#### Task 12: Add Structured Logging

**What to do**:
- [ ] Create structured logging middleware
- [ ] Add logging for all anti-bot events (consent, CAPTCHA, blocks)
- [ ] Log parsing strategy selection and results
- [ ] Add request/response logging (sanitized)
- [ ] Implement log correlation IDs

**Must NOT do**:
- Do NOT log sensitive data (API keys, credentials)
- Do NOT log PII from job listings
- Do NOT use synchronous logging (performance)

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Standalone logging component

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9-11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: None

**References**:
- Logging: Microsoft.Extensions.Logging
- Sanitization: Extension methods for sensitive data

**Acceptance Criteria**:
- [ ] Structured logging middleware implemented
- [ ] Anti-bot events logged
- [ ] Parsing decisions logged
- [ ] Sensitive data sanitized
- [ ] Correlation IDs working

**Commit**:
- Message: `feat(common): add structured logging for scraping operations`
- Files: `src/Platforms/Ghost.Platform.Common/Logging/ScraperLogger.cs`

---

### Phase 4: Integration (Week 4-5)

#### Task 13: Implement Session Token Synchronization

**What to do**:
- [ ] Create token extraction service for browser sessions
- [ ] Implement cookie synchronization between browser and HTTP clients
- [ ] Add token validation and refresh logic
- [ ] Create session state store
- [ ] Integrate with all three platforms

**Must NOT do**:
- Do NOT store tokens indefinitely (security)
- Do NOT share sessions between different proxy locations
- Do NOT ignore token expiration

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `playwright`, `git-master`
- **Reason**: Complex coordination between browser and HTTP

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 14-16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: Tasks 2-4, 5-8

**References**:
- Token Extraction: Glassdoor CSRF extraction logic
- Browser: Playwright cookie APIs
- Storage: In-memory with TTL

**Acceptance Criteria**:
- [ ] Token extraction from browser works
- [ ] Cookie sync between browser/HTTP works
- [ ] Token refresh on expiration
- [ ] Session state store operational
- [ ] All platforms use sync

**Commit**:
- Message: `feat(common): implement session token synchronization`
- Files: `src/Platforms/Ghost.Platform.Common/Session/TokenSynchronizer.cs`

---

#### Task 14: Add Free Third-Party API Fallbacks

**What to do**:
- [ ] Research free job board APIs (Indeed, Glassdoor, Google Jobs alternatives)
- [ ] Implement adapters for free APIs
- [ ] Create fallback orchestration logic
- [ ] Add cost tracking for API usage (even free APIs have limits)
- [ ] Implement fallback decision logic

**Must NOT do**:
- Do NOT use any paid services
- Do NOT make third-party APIs mandatory
- Do NOT proceed without user consent for external services
- Do NOT ignore rate limits of third-party APIs

**Recommended Agent Profile**:
- **Category**: `ultrabrain`
- **Skills**: `git-master`
- **Reason**: External API integration with constraints

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13, 15-16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: None

**References**:
- Free APIs: Research findings on free job board APIs
- Adapters: Pattern for API integration
- Cost: Usage tracking even for free services

**Acceptance Criteria**:
- [ ] Free API research completed
- [ ] Adapters implemented for free APIs
- [ ] Fallback orchestration working
- [ ] Usage tracking operational
- [ ] Decision logic tested

**Commit**:
- Message: `feat(common): add free third-party API fallback providers`
- Files: `src/Platforms/Ghost.Platform.Common/Fallbacks/`

---

#### Task 15: Implement Circuit Breaker Patterns

**What to do**:
- [ ] Add Polly circuit breaker policies
- [ ] Implement half-open state handling
- [ ] Add circuit breaker metrics
- [ ] Configure platform-specific thresholds
- [ ] Add circuit breaker dashboards

**Must NOT do**:
- Do NOT use circuit breakers for transient errors
- Do NOT ignore half-open state in testing
- Do NOT hardcode thresholds

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Polly library integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13-14, 16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: None

**References**:
- Polly: Circuit breaker documentation
- Metrics: Prometheus/Grafana

**Acceptance Criteria**:
- [ ] Circuit breakers implemented
- [ ] Half-open state works
- [ ] Metrics collection operational
- [ ] Thresholds configurable
- [ ] Dashboards created

**Commit**:
- Message: `feat(common): implement circuit breaker patterns for resilience`
- Files: `src/Platforms/Ghost.Platform.Common/Resilience/CircuitBreakers.cs`

---

#### Task 16: Add Monitoring and Alerting

**What to do**:
- [ ] Implement metrics collection (success rate, errors, latency)
- [ ] Create health check endpoints
- [ ] Add alerting rules for critical thresholds
- [ ] Create monitoring dashboards
- [ ] Add log aggregation

**Must NOT do**:
- Do NOT collect PII in metrics
- Do NOT ignore metric cardinality (performance)
- Do NOT use blocking metric reporting

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Monitoring infrastructure

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13-15)
- **Parallel Group**: Wave 4
- **Blocks**: Task 20 (Deployment monitoring)
- **Blocked By**: Task 12 (Structured logging)

**References**:
- Metrics: Prometheus client library
- Health: ASP.NET Core health checks
- Alerting: AlertManager integration

**Acceptance Criteria**:
- [ ] Metrics collection implemented
- [ ] Health checks operational
- [ ] Alerting rules configured
- [ ] Dashboards created
- [ ] Log aggregation working

**Commit**:
- Message: `feat(common): add monitoring and alerting infrastructure`
- Files: `src/Platforms/Ghost.Platform.Common/Monitoring/`

---

### Phase 5: Testing & Deployment (Week 5-6)

#### Task 17: Comprehensive Integration Testing

**What to do**:
- [ ] Write integration tests for all platforms
- [ ] Test proxy rotation scenarios
- [ ] Test consent page handling
- [ ] Test fallback strategies
- [ ] Test error recovery
- [ ] Test parsing strategies
- [ ] Benchmark performance

**Must NOT do**:
- Do NOT skip mocking external services
- Do NOT test against production without consent
- Do NOT ignore test flakiness

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Comprehensive testing required

**Parallelization**:
- **Can Run In Parallel**: NO (sequential testing)
- **Parallel Group**: Wave 5
- **Blocks**: Task 20
- **Blocked By**: Tasks 9-16

**References**:
- Tests: All existing test projects
- Mocking: Moq, WireMock
- Benchmarking: BenchmarkDotNet

**Acceptance Criteria**:
- [ ] >90% code coverage
- [ ] All integration tests pass
- [ ] Proxy rotation tested
- [ ] Consent handling tested
- [ ] Fallbacks tested
- [ ] Performance benchmarks recorded

**Commit**:
- Message: `test(all): add comprehensive integration tests`
- Files: `tests/**/*Tests.cs`

---

#### Task 18: Performance Benchmarking

**What to do**:
- [ ] Benchmark request latency
- [ ] Benchmark parsing performance
- [ ] Benchmark proxy rotation overhead
- [ ] Compare before/after metrics
- [ ] Document performance characteristics

**Must NOT do**:
- Do NOT benchmark in production
- Do NOT ignore cold start times
- Do NOT use unrealistic test data

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Benchmarking

**Parallelization**:
- **Can Run In Parallel**: YES (with Task 17)
- **Parallel Group**: Wave 5
- **Blocks**: None
- **Blocked By**: None

**Acceptance Criteria**:
- [ ] Latency benchmarks recorded
- [ ] Parsing performance measured
- [ ] Overhead quantified
- [ ] Comparison documented
- [ ] Performance goals met

**Commit**:
- Message: `perf(all): add performance benchmarks and documentation`
- Files: `benchmarks/`

---

#### Task 19: Documentation and Runbooks

**What to do**:
- [ ] Write architecture documentation
- [ ] Create troubleshooting runbooks
- [ ] Document configuration options
- [ ] Add deployment guides
- [ ] Create incident response procedures

**Must NOT do**:
- Do NOT skip security considerations
- Do NOT ignore legal/ethical guidelines
- Do NOT use outdated screenshots

**Recommended Agent Profile**:
- **Category**: `writing`
- **Skills**: `git-master`
- **Reason**: Documentation

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 17-18)
- **Parallel Group**: Wave 5
- **Blocks**: Task 20
- **Blocked By**: None

**Acceptance Criteria**:
- [ ] Architecture docs complete
- [ ] Runbooks created
- [ ] Configuration documented
- [ ] Deployment guides ready
- [ ] Incident procedures defined

**Commit**:
- Message: `docs(all): add comprehensive documentation and runbooks`
- Files: `docs/`

---

#### Task 20: Production Deployment with Canary

**What to do**:
- [ ] Create canary deployment configuration
- [ ] Set up feature flags
- [ ] Configure rollback procedures
- [ ] Deploy to production
- [ ] Monitor metrics during rollout
- [ ] Gradually increase traffic
- [ ] Validate success metrics

**Must NOT do**:
- Do NOT deploy without rollback plan
- Do NOT skip monitoring during rollout
- Do NOT ignore error rate spikes

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Production deployment

**Parallelization**:
- **Can Run In Parallel**: NO
- **Parallel Group**: Wave 5
- **Blocks**: None (final task)
- **Blocked By**: Tasks 17-19

**Acceptance Criteria**:
- [ ] Canary deployed successfully
- [ ] Feature flags operational
- [ ] Rollback tested
- [ ] Metrics validated
- [ ] Full deployment complete
- [ ] Success metrics met (>95% success rate)

**Commit**:
- Message: `deploy(all): production deployment with canary rollout`
- Files: `deploy/`

---

## Success Criteria

### Verification Commands

```bash
# Run all tests
dotnet test tests/Ghost.Platform.Indeed.Tests
dotnet test tests/Ghost.Platform.Glassdoor.Tests
dotnet test tests/Ghost.Platform.Google.Tests

# Run integration tests
dotnet test tests/Integration.Tests

# Check code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run benchmarks
dotnet run --project benchmarks/Ghost.Benchmarks

# Verify metrics endpoint
curl http://localhost:5000/metrics
```

### Final Checklist

- [ ] All TODOs complete
- [ ] All tests passing
- [ ] Code coverage >90%
- [ ] Documentation complete
- [ ] Production deployed
- [ ] Success rate >95% (measured over 7 days)
- [ ] Error rate <1%
- [ ] Consent handling >90% success
- [ ] Monitoring operational
- [ ] Runbooks tested

---

## Appendix: Implementation Details

### Platform-Specific Code Locations

**Indeed**:
- API Client: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`
- Options: `/src/Platforms/Ghost.Platform.Indeed/IndeedOptions.cs`
- Tests: `/tests/Ghost.Platform.Indeed.Tests/`

**Glassdoor**:
- API Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs`
- Browser Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorBrowserClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`
- Tests: `/tests/Platforms/Ghost.Platform.Glassdoor.Tests/`

**Google Jobs**:
- API Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`
- Browser Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`
- Tests: `/tests/Ghost.Platform.Google.Tests/`

**Common**:
- Session: `/src/Platforms/Ghost.Platform.Common/Session/`
- Proxies: `/src/Core/Ghost/Services/`
- Monitoring: `/src/Platforms/Ghost.Platform.Common/Monitoring/`

### Free Third-Party APIs

**Job Board APIs**:
- USAJOBS API (federal jobs): https://developer.usajobs.gov/
- Reed API (UK jobs): https://www.reed.co.uk/developers
- Adzuna API (multiple countries): https://developer.adzuna.com/
- Jooble API (multiple countries): https://jooble.org/api/about
- Indeed Publisher API (deprecated but some alternatives exist)

**Search APIs**:
- Google Custom Search API (free tier): https://developers.google.com/custom-search
- Bing Search API (free tier): https://www.microsoft.com/en-us/bing/apis/bing-web-search-api

### Configuration Schema

```json
{
  "JobScrapers": {
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

## Notes

### Anti-Patterns Avoided

1. **No single point of failure**: Multiple proxy providers, fallback strategies
2. **No hardcoded secrets**: All credentials externalized to configuration
3. **No tight coupling**: Platforms independent, common infrastructure shared
4. **No silent failures**: Structured logging and error reporting throughout
5. **No breaking changes**: Backward compatible with existing APIs

### Performance Considerations

- Connection pooling via RotatingProxySession
- Async/await throughout
- Minimal allocations in hot paths
- Batch processing where possible
- Caching of session tokens

### Legal/Ethical Considerations

- Respect robots.txt
- Implement rate limiting
- Consider official APIs where available
- Document scraping activities
- Comply with platform ToS
- Handle consent appropriately

---

**Ready to execute**: Run `/start-work` to begin implementation