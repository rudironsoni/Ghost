# Ghost Testing Lanes: Lane-Based Parallelization Policy

## Overview

This RFC defines the lane-based parallelization policy for the Ghost test suite, replacing the previous blanket serialization approach (`MaxCpuCount=1`, global parallelization disabled).

**Status:** Implemented  
**Version:** 1.0  
**Date:** 2026-02-10

## Problem Statement

The previous Ghost.runsettings configuration forced global serialization:
- `MaxCpuCount=1`
- `DisableParallelization=true`
- All `xunit.runner.json` files had `parallelizeAssembly=false`, `maxParallelThreads=1`

This approach:
- Masked isolation problems and shared state bugs
- Slowed feedback cycles significantly
- Prevented optimal resource utilization
- Created no incentive for proper test isolation

## Solution: Lane-Based Parallelization

Tests are now organized into **four execution lanes** with distinct parallelization policies based on their isolation guarantees and external dependencies.

### Lane Matrix

| Lane | Category | Parallelization | Max Threads | Shared State | External Dependencies | Use Case |
|------|----------|-----------------|-------------|--------------|----------------------|----------|
| **A** | Unit | **High** | Unlimited | ❌ None | ❌ None | Pure logic, in-memory |
| **B** | Integration | **Medium** | 4 | ⚠️ Isolated per test | ⚠️ Mocked only | WireMock, in-process |
| **C** | System | **Controlled** | 4 | ⚠️ Collection fixtures | ⚠️ Synthetic browser | Playwright + mock server |
| **D** | End2End | **Sequential** | 1 | ⚠️ Provider state | ⚠️ Live APIs | Live provider tests |

### Lane A: Unit (High Parallel)

**Characteristics:**
- Pure unit tests, no external IO
- No shared mutable state
- CPU-bound computation only
- Fastest feedback

**Configuration:**
```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true
}
```

**Test Projects:**
- `tests/Unit/**/*.Unit.Tests`
- `tests/Contracts/**/*.Tests`
- `tests/Core/Ghost.Tests`
- `tests/Platforms/**/*.Tests` (not Integration/End2End)
- `tests/Hosting/**/*.Tests`

**Requirements:**
- ✅ No file system access
- ✅ No network access
- ✅ No database access
- ✅ No shared mutable static state
- ✅ Deterministic execution order independence

---

### Lane B: Integration (Medium Parallel)

**Characteristics:**
- Tests with mocked external dependencies
- Isolated WireMock instances per test class
- No live network calls
- Deterministic but IO-bound

**Configuration:**
```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4
}
```

**Test Projects:**
- `tests/Platforms/**/*.Integration`
- `tests/Integration/**/*.Integration.Tests`

**Requirements:**
- ✅ WireMock.NET for HTTP mocking
- ✅ Isolated mock server per test class
- ✅ Port allocation strategy (dynamic or pool)
- ✅ No live external APIs
- ⚠️ Cleanup guarantees (dispose WireMock instances)

**Port Allocation Strategy:**
```csharp
// Example: Per-test WireMock isolation
public class ProviderIntegrationTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    
    public ProviderIntegrationTests()
    {
        _mockServer = WireMockServer.Start(); // Ephemeral port
    }
    
    public void Dispose() => _mockServer?.Stop();
}
```

---

### Lane C: System (Controlled Parallel)

**Characteristics:**
- Browser-based tests with synthetic scenarios
- Shared browser process, isolated contexts
- Collection fixtures for expensive resources
- Deterministic synthetic environments

**Configuration:**
```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 4
}
```

**Test Projects:**
- `tests/SDK/Ghost.Sdk.Spider.Tests`
- Future: `tests/System/**/*.System.Tests`

**Requirements:**
- ✅ Playwright browser context isolation
- ✅ Synthetic web scenario server (local, no internet)
- ✅ Collection fixtures for browser/server lifecycle
- ⚠️ Context cleanup per test
- ⚠️ Shared resources managed via xUnit collections

**Example Collection Fixture:**
```csharp
[CollectionDefinition("Browser")]
public class BrowserCollection : ICollectionFixture<BrowserFixture> { }

public class BrowserFixture : IAsyncLifetime
{
    public IBrowser Browser { get; private set; }
    
    public async Task InitializeAsync()
    {
        var playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync();
    }
    
    public Task DisposeAsync() => Browser.DisposeAsync().AsTask();
}

[Collection("Browser")]
public class SystemTests
{
    private readonly BrowserFixture _fixture;
    
    public SystemTests(BrowserFixture fixture) => _fixture = fixture;
    
    [Fact]
    public async Task Test_IsolatedContext()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        // Each test gets its own isolated context
    }
}
```

---

### Lane D: End2End (Sequential)

**Characteristics:**
- Live provider API tests
- Real external dependencies
- Provider rate limit compliance
- Non-deterministic (network, provider state)

**Configuration:**
```json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1
}
```

**Test Projects:**
- `tests/Plugins/**/*.End2EndTests`
- `tests/End2End/**/*.End2End.Tests`

**Requirements:**
- ⚠️ Live API credentials required
- ⚠️ Rate limit compliance
- ⚠️ Sequential execution to avoid conflicts
- ⚠️ Non-blocking CI by default (manual trigger or nightly)

**CI Strategy:**
- **PR Gate:** ❌ Excluded (use `--filter "Category!=End2End"`)
- **Merge Gate:** ❌ Excluded
- **Nightly/Manual:** ✅ Enabled

---

## Configuration Files

### Ghost.runsettings (Root Level)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!--
    Lane-Based Parallelization Policy
    ==================================
    
    This runsettings file now enables assembly-level parallelism.
    Individual test projects control their own parallelization via xunit.runner.json.
    
    See: docs/rfc/testing-lanes.md for complete policy
  -->
  <RunConfiguration>
    <!-- Enable assembly-level parallelism -->
    <TestSessionTimeout>300000</TestSessionTimeout>
  </RunConfiguration>
  <MSBuild>
    <NodeReuse>false</NodeReuse>
  </MSBuild>
</RunSettings>
```

### xunit.runner.json (Per-Project)

Each test project contains an `xunit.runner.json` file with lane-specific settings. See lane matrix above for configurations.

---

## CI Integration

### PR Gate (Deterministic, Fast)

```bash
dotnet test Ghost.sln --no-build --filter "Category!=End2End" --settings Ghost.runsettings
```

**Includes:**
- ✅ Lane A (Unit)
- ✅ Lane B (Integration)
- ✅ Lane C (System)
- ❌ Lane D (End2End)

**Expected Duration:** < 5 minutes (with parallelization)

### Merge Gate (Comprehensive)

Same as PR gate. End2End tests are optional and non-blocking.

### Nightly/Manual (Full Suite)

```bash
dotnet test Ghost.sln --no-build --settings Ghost.runsettings
```

**Includes:** All lanes (A, B, C, D)

**Expected Duration:** 10-30 minutes (provider-dependent)

---

## Migration Path

### Phase 1: Enable Parallelization (Current)
- ✅ Remove global serialization from Ghost.runsettings
- ✅ Update xunit.runner.json per lane
- ✅ Document lane policy

### Phase 2: Isolate Shared State (Next)
- Audit tests for shared mutable state
- Refactor to use immutable fixtures or per-test isolation
- Add analyzers to detect shared state violations

### Phase 3: Optimize Resource Allocation (Future)
- Dynamic port allocation for WireMock
- Shared browser pool with context isolation
- Adaptive thread limits based on CI environment

---

## Acceptance Criteria

- ✅ Lane A (Unit) runs with full parallelism
- ✅ Lane B (Integration) runs with controlled parallelism (4 threads)
- ✅ Lane C (System) runs with collection-level isolation
- ✅ Lane D (End2End) runs sequentially
- ✅ No mutable shared state in parallel lanes
- ✅ Deterministic runs in PR lanes (Unit + Integration)
- ✅ AGENTS verification passes:
  - `dotnet format Ghost.sln --verify-no-changes`
  - `dotnet restore Ghost.sln`
  - `dotnet build Ghost.sln --no-restore --warnaserror`
  - `dotnet test Ghost.sln --no-build --filter "Category!=End2End"`

---

## Risk Assessment

**Risk Tier:** High  
**Blast Radius:** All test execution, CI pipelines, potential test interdependencies revealed

### Known Risks
1. **Shared State Exposure:** Tests previously passing due to serialization may now fail
2. **Resource Contention:** WireMock port conflicts, browser resource limits
3. **CI Instability:** Parallel execution may expose race conditions

### Mitigation
- Incremental rollout (start with Lane A, then B, then C)
- Dedicated shared state audit issue
- Rollback plan: `git checkout Ghost.runsettings.backup`

---

## Rollback Plan

### Immediate Rollback (< 5 minutes)
```bash
# Restore backup
cp Ghost.runsettings.backup Ghost.runsettings

# Revert all xunit.runner.json files
find tests -name "xunit.runner.json" -exec sed -i 's/"parallelizeAssembly": true/"parallelizeAssembly": false/g' {} \;
find tests -name "xunit.runner.json" -exec sed -i 's/"parallelizeTestCollections": true/"parallelizeTestCollections": false/g' {} \;

# Commit revert
git add Ghost.runsettings tests/**/xunit.runner.json
git commit -m "Rollback: Revert lane-based parallelization"
git push
```

### Verification After Rollback
```bash
dotnet test Ghost.sln --no-build --filter "Category!=End2End"
```

---

## Follow-up Issues

- **Ghost-xyz:** Audit shared mutable state across all test projects
- **Ghost-abc:** Implement dynamic WireMock port allocation strategy
- **Ghost-def:** Add analyzer to detect shared state violations in Unit tests

---

## References

- **Parent Epic:** Ghost-0vm (Enterprise-grade Test Overhaul)
- **Related Issues:**
  - Ghost-w4y (Normalize test categories)
  - Ghost-qzwv (WireMock.NET standardization)
  - Ghost-zye4 (CI lane refactor)
- **xUnit Parallelization Docs:** https://xunit.net/docs/running-tests-in-parallel
- **Playwright Context Isolation:** https://playwright.dev/dotnet/docs/browser-contexts

---

## Approval

**Status:** Implemented  
**Approved By:** Rudimar Ronsoni  
**Implementation Date:** 2026-02-10
