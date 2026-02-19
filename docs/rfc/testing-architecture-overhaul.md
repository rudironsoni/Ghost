# Testing Architecture Overhaul - Deterministic Test Lanes

## Executive Summary

This RFC describes the refactoring of CI pipelines into deterministic test lanes to improve developer velocity, reduce flakiness, and provide clear separation of concerns across different test categories and capabilities.

### Problem Statement

The current CI configuration has several issues:
- Single test command runs all tests together
- PR gate depends on live providers, causing flaky failures
- No separation by capability or reliability
- Slow feedback loops for developers
- Difficult to triage failures due to mixed test types

### Goals and Success Criteria

**Goals:**
1. Separate tests into deterministic lanes based on category and capability
2. Remove live provider dependencies from PR blocking lane
3. Provide fast feedback for developers
4. Enable clear failure triage per lane
5. Support different parallelism strategies per lane

**Success Criteria:**
- PR no longer blocked by live provider failures
- Deterministic lane pass/fail semantics
- Clear separation of concerns
- All lanes run AGENTS verification
- Documentation complete

### Scope and Out-of-Scope

**In Scope:**
- CI workflow refactoring into 3 lanes
- Trait-based test selection
- Artifact retention policies per lane
- Documentation updates

**Out of Scope:**
- Test implementation changes
- New test frameworks
- Provider contract enforcement (separate issue)
- Failure diagnostics standardization (separate issue)

## Taxonomy and Capability Model

### Test Categories

| Category | Description | Examples | Lane |
|----------|-------------|----------|------|
| Unit | Hermetic tests with no external I/O | Pure functions, logic tests | A |
| Integration | Tests with mocked dependencies | WireMock-based integration tests | A, B |
| System | Tests with synthetic browser + mock server | End-to-end scenarios with controlled environment | B |
| End2End | Tests with live providers | Real external APIs, real browsers | C |

### Capability Traits

| Trait | Description | Lane |
|-------|-------------|------|
| RequiresMockServer | Uses WireMock.NET for HTTP mocking | A |
| RequiresSyntheticServer | Uses synthetic scenario server | B |
| RequiresProviderLive | Requires live provider access | C |
| RequiresBrowser | Requires browser automation | B, C |

### Category Normalization Rules

1. **Unit Tests**: Must be hermetic (no network, file system, or external dependencies)
2. **Integration Tests**: Must use mocked dependencies (WireMock.NET or synthetic server)
3. **System Tests**: Use synthetic browser + mock server for deterministic behavior
4. **End2End Tests**: Only for live provider smoke tests and critical path validation

## Test Topology

### Project Structure

```
Ghost/
├── src/
│   ├── Core/Ghost/              # Layer 0
│   ├── Contracts/               # Layer 1
│   ├── Platforms/               # Layer 2
│   ├── Hosting/                 # Layer 3
│   └── Sdk/                     # Layer 4
└── tests/
    ├── Unit/                    # Unit tests
    ├── Integration/             # Integration tests (mocked)
    ├── System/                  # System tests (synthetic)
    └── End2End/                 # End2End tests (live)
```

### Migration from Current to Target State

**Current State:**
- Single test command runs all tests
- Mixed categories in same workflow
- No capability-based filtering

**Target State:**
- 3 separate lanes with clear boundaries
- Trait-based test selection
- Capability-aware filtering

**Migration Steps:**
1. Create new lane workflows (Lane A, B, C)
2. Update existing workflows to reference new lanes
3. Add trait attributes to tests
4. Verify test distribution across lanes
5. Remove old test commands from workflows

### Non-Test Project Exclusion

The following projects are excluded from test lanes:
- `temp-test/` - Temporary test directories
- `TestLinkedInScraper/` - Manual testing tool
- `TestGoogleJobsApp/` - Manual testing tool
- `DebugScraper/` - Debugging tool
- `RealScrapingVerification/` - Manual verification tool
- `GlassdoorTest/` - Temporary test directory

## Parallelization Matrix

### Lane Definitions

#### Lane A - PR Blocking (Fast, Deterministic)

**Scope:**
- Unit tests (hermetic, no external I/O)
- Integration tests with mocked dependencies
- No live network calls
- Max parallelism

**Characteristics:**
- Fast execution (< 15 minutes)
- Deterministic results
- No external dependencies
- Must pass for PR merge

**Test Filter:**
```
Category=Unit OR (Category=Integration AND Capability=RequiresMockServer)
```

**Parallelism:** Max (no limits)

**Artifact Retention:** 7 days

#### Lane B - Merge Gate (Controlled)

**Scope:**
- System tests with synthetic browser + mock server
- Deterministic but slower
- Shared resource tests
- Must pass for merge to main

**Characteristics:**
- Medium execution (< 30 minutes)
- Deterministic results
- Controlled parallelism
- Must pass for merge

**Test Filter:**
```
Category=System AND Capability=RequiresSyntheticServer
```

**Parallelism:** Controlled (limited to avoid resource contention)

**Artifact Retention:** 14 days

#### Lane C - Nightly/Live (Non-blocking)

**Scope:**
- End2End tests with live providers
- Real external APIs
- Non-blocking for PR/merge by default
- Can be triggered manually
- Failure signals provider drift

**Characteristics:**
- Slow execution (< 90 minutes)
- Non-deterministic (live providers)
- Sequential/low parallelism
- Non-blocking for PR/merge

**Test Filter:**
```
Category=End2End OR Capability=RequiresProviderLive
```

**Parallelism:** Low (sequential or limited parallelism)

**Artifact Retention:** 30 days

### Resource Isolation Guarantees

| Lane | Network | Browser | Mock Server | Live Providers |
|------|---------|---------|-------------|----------------|
| A | None | None | WireMock.NET | None |
| B | None | Synthetic | Synthetic Server | None |
| C | Live | Real | None | Live |

### Concurrency Levels per Lane

| Lane | Max Parallel Jobs | Job Timeout |
|------|-------------------|-------------|
| A | Unlimited | 15-20 min |
| B | Controlled (2-4) | 30 min |
| C | Low (1-2) | 90 min |

## Mock Platform Architecture

### WireMock.NET Standardization

**Purpose:** Provide deterministic HTTP mocking for integration tests

**Usage:**
- Integration tests in Lane A
- Provider contract testing
- API response simulation

**Configuration:**
```csharp
[Trait("Category", "Integration")]
[Trait("Capability", "RequiresMockServer")]
[Collection("ProviderIntegration")]
public class ProviderIntegrationTests
{
    private readonly WireMockFixture _fixture;

    public ProviderIntegrationTests(WireMockFixture fixture)
    {
        _fixture = fixture;
    }
}
```

### Synthetic Scenario Server

**Purpose:** Provide deterministic browser automation scenarios

**Usage:**
- System tests in Lane B
- Complex scenario testing (consent, pagination, infinite scroll)
- Browser interaction simulation

**Configuration:**
```csharp
[Trait("Category", "System")]
[Trait("Capability", "RequiresSyntheticServer")]
[Collection("SystemTests")]
public class SystemTests
{
    private readonly SyntheticServerFixture _fixture;

    public SystemTests(SyntheticServerFixture fixture)
    {
        _fixture = fixture;
    }
}
```

### When to Use Which Approach

| Scenario | Approach | Lane |
|----------|----------|------|
| API response mocking | WireMock.NET | A |
| Browser automation with controlled HTML | Synthetic Server | B |
| Live provider testing | Real providers | C |
| Pure logic testing | No mocking | A |

## Provider Contract Model

### Contract Definitions

Provider contracts define the expected behavior and responses from external providers.

**Example:**
```csharp
public interface IGoogleJobsContract
{
    Task<JobSearchResponse> SearchJobsAsync(JobSearchCriteria criteria);
}
```

### Enforcement Mechanisms

1. **Contract Tests:** Verify provider behavior matches contract
2. **Mock Servers:** Enforce contract in integration tests
3. **Live Smoke Tests:** Verify provider still matches contract

### Compliance Reporting

- Contract violations create issues
- Non-compliant providers are flagged
- Compliance tracked in dashboard

## Complex Scenario Coverage

### Consent Flows

**Scenario Families:**
1. Blocking modal (accept/reject/no-action)
2. Soft banner (non-blocking)
3. Iframe consent manager
4. Region-specific consent (GDPR, CCPA, LGPD)
5. Stateful consent (persistence across session)

**Lane:** B (System tests with synthetic server)

### Infinite Scroll

**Scenario Variants:**
1. Auto-fetch on threshold
2. Button-driven loads
3. Virtualized DOM replacements
4. Duplicate chunk replay

**Lane:** B (System tests with synthetic server)

### Pagination

**Scenario Variants:**
1. Numbered pages
2. Cursor/next-token APIs
3. Mixed server/client paging
4. Loop and dead-end protection

**Lane:** B (System tests with synthetic server)

### Deduplication

**Scenario Variants:**
1. Query reordering
2. Tracking parameters (UTM, session IDs)
3. Redirect chains
4. Same posting multiple aliases
5. Temporal changes

**Lane:** B (System tests with synthetic server)

### Rate Limiting / Anti-Bot

**Scenario Variants:**
1. Rate limit detection
2. CAPTCHA handling
3. IP blocking
4. User agent detection

**Lane:** C (End2End tests with live providers)

## CI Lane Governance

### PR Gate Requirements

**Lane A (PR Blocking):**
- Must pass before PR can be merged
- Runs on every PR
- Fast feedback (< 15 minutes)
- No live dependencies

**Required Jobs:**
- Validate PR Title
- Lint & Format Check
- Build Solution
- Unit Tests (Hermetic)
- Mocked Integration Tests
- Check DotnetSpider References

### Merge Gate Requirements

**Lane B (Merge Gate):**
- Must pass before merge to main
- Runs on push to main
- Medium feedback (< 30 minutes)
- Deterministic with synthetic environment

**Required Jobs:**
- Build Solution
- System Tests (Synthetic)
- Synthetic Integration Tests

### Live Provider Smoke Policy

**Lane C (Nightly/Live):**
- Non-blocking for PR/merge
- Runs on schedule (daily at 3 AM UTC)
- Can be triggered manually
- Failure signals provider drift

**Jobs:**
- End2End Tests (Live Providers)
- Live Provider Tests
- Performance Benchmarks
- Stability Tests

### Artifact and Diagnostics Standards

**Lane A (PR Blocking):**
- Test results (TRX)
- Coverage reports
- Retention: 7 days

**Lane B (Merge Gate):**
- Test results (TRX)
- Coverage reports
- Screenshots on failure
- Retention: 14 days

**Lane C (Nightly/Live):**
- Test results (TRX)
- Screenshots on failure
- Video recordings
- HAR files
- Browser traces
- Retention: 30 days

## Flake Governance

### Budgets and Thresholds

**Flake Budget:**
- Target: < 0.5% flake rate over 14 days
- Alert on exceedance
- Weekly flake review meeting

**Thresholds:**
- Lane A: 0% tolerance (must be deterministic)
- Lane B: < 0.1% tolerance (synthetic environment)
- Lane C: < 1% tolerance (live providers)

### Quarantine Process

**Quarantine Requirements:**
- Owner assignment
- Expiry date (max 30 days)
- Linked issue with root cause analysis
- RCA template completed

**Quarantine Lane:**
- Tests that fail intermittently moved to quarantine
- Still run but don't block PR
- Must have expiration
- Owner must provide fix plan

### RCA Requirements

**RCA Template:**
1. Trigger condition
2. Why not caught earlier
3. Deterministic reproduction steps
4. Permanent fix proposal
5. Owner and SLA for fix

## Migration and Rollback Plan

### Phase-by-Phase Migration

**Phase 1: Create New Lane Workflows**
- Create lane-a-pr-blocking.yml
- Create lane-b-merge-gate.yml
- Create lane-c-nightly.yml
- Test workflows in isolation

**Phase 2: Update Existing Workflows**
- Update ci.yml to reference new lanes
- Update pr-validation.yml to reference Lane A
- Update nightly.yml to reference Lane C
- Verify workflow orchestration

**Phase 3: Add Trait Attributes**
- Add Category traits to all tests
- Add Capability traits to integration/system tests
- Verify test distribution across lanes

**Phase 4: Monitor and Adjust**
- Monitor lane execution times
- Adjust parallelism settings
- Fine-tune test filters
- Update documentation

### Rollback Procedures per Component

**Lane Workflows:**
- Rollback: Delete new lane workflows
- Restore: Restore old workflow files from git
- Impact: PRs blocked by live provider failures

**Test Traits:**
- Rollback: Remove trait attributes from tests
- Restore: Revert test file changes
- Impact: Tests run in all lanes

**Workflow Orchestration:**
- Rollback: Restore old workflow files
- Restore: Revert workflow changes
- Impact: Single test command runs all tests

### Risk Mitigation

**Risk 1: Tests not properly categorized**
- Mitigation: Run all lanes in parallel during migration
- Fallback: Use broad filters to ensure all tests run

**Risk 2: Lane execution times exceed expectations**
- Mitigation: Monitor execution times and adjust parallelism
- Fallback: Increase timeout values

**Risk 3: Live provider failures block PRs**
- Mitigation: Ensure Lane A has no live dependencies
- Fallback: Use continue-on-error for Lane C

## References

### Scrapy Test Architecture
- https://docs.scrapy.org/en/latest/topics/practices.html#testing

### Current Ghost Test State
- See: docs/test-tier-audit.md

### Related Issues and Dependencies
- Ghost-r26: Refactor CI into deterministic test lanes
- Ghost-zye4: Refactor CI into deterministic test lanes
- Ghost-3z6v: Standardize failure diagnostics artifact capture
- Ghost-rnmt: Implement flaky test governance with quarantine
- Ghost-bz2c: Publish enterprise test architecture RFC

## Appendix

### Workflow File Locations

```
.github/workflows/
├── ci.yml                      # Main integration orchestrator
├── pr-validation.yml           # PR validation (quick checks)
├── nightly.yml                 # Nightly build orchestrator
├── lane-a-pr-blocking.yml      # Lane A: PR blocking
├── lane-b-merge-gate.yml       # Lane B: Merge gate
├── lane-c-nightly.yml          # Lane C: Nightly/live
├── release.yml                 # Release workflow
└── security-scan.yml           # Security scans
```

### Test Filter Reference

**Lane A (PR Blocking):**
```bash
dotnet test Ghost.sln --filter "Category=Unit|Category=UnitTest"
dotnet test Ghost.sln --filter "Category=Integration&Capability=RequiresMockServer"
```

**Lane B (Merge Gate):**
```bash
dotnet test Ghost.sln --filter "Category=System&Capability=RequiresSyntheticServer"
dotnet test Ghost.sln --filter "Category=Integration&Capability=RequiresSyntheticServer"
```

**Lane C (Nightly/Live):**
```bash
dotnet test Ghost.sln --filter "Category=End2End"
dotnet test Ghost.sln --filter "Capability=RequiresProviderLive"
```

### Artifact Retention Policy

| Lane | Test Results | Coverage | Screenshots | Videos | HAR | Traces | Retention |
|------|--------------|----------|-------------|--------|-----|--------|-----------|
| A | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | 7 days |
| B | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | 14 days |
| C | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ | 30 days |

---

**Status:** Draft
**Last Updated:** 2026-02-11
**Author:** Rudimar Ronsoni
**Reviewers:** TBD
