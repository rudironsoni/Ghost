# Task.Delay Analysis in Test Files

## Summary

- **Total violations fixed:** 2 test files modified (SessionManagerTests.cs, ProxyManagerTests.cs)
- **Remaining Task.Delays:** 32 occurrences (all documented as acceptable below)
- **Production code changes:** Added TimeProvider support to BrowserSession, SessionManager, SessionManagerOptions, ProxyManager, and ProxyOptions

## Fixed Violations

### 1. SessionManagerTests.cs
- **Before:** `await Task.Delay(10);` to wait for session expiration
- **After:** Uses `FakeTimeProvider` to advance time deterministically
- **Files modified:**
  - `src/Kernel/Ghost/Session/BrowserSession.cs` - Added `IsExpired(TimeProvider)` overload
  - `src/Kernel/Ghost/Session/SessionManagerOptions.cs` - Added `TimeProvider` property
  - `src/Kernel/Ghost/Session/SessionManager.cs` - Uses `TimeProvider` for time operations

### 2. ProxyManagerTests.cs
- **Before:** `await Task.Delay(150);` to wait for retry period
- **After:** Uses `FakeTimeProvider` to advance time deterministically
- **Files modified:**
  - `src/Sdk/Ghost.Sdk/Middleware/ProxyOptions.cs` - Added `TimeProvider` property
  - `src/Sdk/Ghost.Sdk/Middleware/ProxyManager.cs` - Uses `TimeProvider` for time operations

## Acceptable Task.Delays (32 occurrences)

These remaining Task.Delays are acceptable for the following reasons:

### 1. E2E/Browser Integration Tests (Real Waits Required)
These tests interact with real browser instances and require actual delays for:
- JavaScript execution
- DOM updates
- Network operations
- Page loads

**Files:**
- `ConsentScenarioTests.cs` (2) - Polling loops for UI element visibility
- `ScrollScenarioTests.cs` (1) - Polling for JavaScript conditions
- `PaginationScenarioTests.cs` (2) - Browser page interactions
- `ScrollScenarioTests.cs` (6) - E2E scroll scenario testing

### 2. Mock/Stub Setup (Simulating Real Behavior)
Task.Delays in mock setups simulate real-world timing behavior:

**Files:**
- `WebSocketAdapterTests.cs` (1) - Simulating server-side message delays
- `GraphQLAdapterExtractTests.cs` (1) - Simulating slow HTTP responses for timeout testing
- `RobotsMiddlewareTests.cs` (1) - Simulating network delays in HTTP handler mocks
- `AssuranceCanaryRunnerTests.cs` (1) - Mocking long-running operations for cancellation testing

### 3. Test Infrastructure (Framework Code)
These are part of testing framework/infrastructure, not test code:

**Files:**
- `AsyncTestHelpers.cs` (3) - Helper methods for polling, timeouts, and retries
- `TestTimeoutAttribute.cs` (1) - xUnit timeout enforcement infrastructure
- `ReliabilityConfiguration.cs` (1) - Browser process cleanup delay

### 4. Benchmarks (Performance Testing)
Task.Delays are part of benchmark workload simulation:

**Files:**
- `GhostEngineBenchmarks.cs` (4) - ConcurrentBag vs Channel performance comparison

### 5. Scenario Server (Test Data Simulation)
These simulate network delays in the test scenario server:

**Files:**
- `ScrollScenarios.cs` (2) - Simulating API response delays

### 6. Concurrency/Parallel Tests
Task.Delays used to simulate concurrent work:

**Files:**
- `AggregatedJobClientIntegrationTests.cs` (2) - Testing parallel execution timing
- `SpiderEngineTests.cs` (2) - Background task coordination with FakeTimeProvider
- `ParallelExecutionTests.cs` (1) - Concurrent execution simulation
- `DistributedLockTests.cs` (1) - Brief delay for task startup synchronization

## Verification

To verify no new violations are introduced, run:

```bash
# Count Task.Delays (should be 32)
grep -r "Task\.Delay" tests/ --include="*.cs" | grep -v "obj\|bin\|FakeTimeProvider\|_timeProvider" | wc -l

# List all occurrences
grep -rn "Task\.Delay" tests/ --include="*.cs" | grep -v "obj\|bin\|FakeTimeProvider\|_timeProvider"
```

## Design Principles

1. **Real Test Code** (testing production logic): Must use FakeTimeProvider
2. **Test Infrastructure** (helpers, timeout, benchmarks): Real delays acceptable
3. **Mock Setup** (simulating external behavior): Real delays acceptable
4. **E2E Tests** (browser interaction): Real delays required
