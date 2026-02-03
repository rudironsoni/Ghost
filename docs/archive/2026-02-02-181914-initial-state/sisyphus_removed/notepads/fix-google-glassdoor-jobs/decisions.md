# Task 4.1: Retry with Exponential Backoff - Decisions

## Date: 2026-01-31

## Architectural Decisions

### 1. Use Existing Polly Infrastructure
**Decision:** Leverage existing Polly-based retry policies in `Ghost.Http` namespace rather than creating a new retry mechanism from scratch.

**Rationale:**
- Polly is already a dependency in Ghost.Core
- Existing patterns (`RetryPolicy.cs`, `HttpClientPollyExtensions.cs`) provide a solid foundation
- Reduces code duplication and maintenance burden
- Polly is battle-tested and widely used in .NET ecosystem

### 2. Create Enhanced Retry Policy as Separate Utility
**Decision:** Create `EnhancedRetryPolicy.cs` as a new utility class rather than modifying existing `RetryPolicy.cs`.

**Rationale:**
- Existing `RetryPolicy.cs` is simple and widely used
- Enhanced version adds complexity (different error types, jitter, logging)
- Allows gradual migration - existing code continues to work
- Clear separation of concerns: basic vs advanced retry

### 3. Different Retry Counts for Different Platforms
**Decision:** Use 3 retries for Google Jobs, 4 retries for Glassdoor.

**Rationale:**
- Glassdoor has more aggressive rate limiting (observed in existing code)
- Google Jobs has browser fallback, so fewer retries needed
- Allows platform-specific tuning without complex configuration

### 4. Jitter Enabled by Default
**Decision:** Enable jitter by default (`enableJitter: true`) in both platforms.

**Rationale:**
- Prevents thundering herd effect when multiple instances retry simultaneously
- Critical for distributed systems or high-traffic scenarios
- Minimal performance impact (random 250-1000ms added to delay)
- Can be disabled via configuration if needed

### 5. LoggerMessage Delegates for Performance
**Decision:** Use LoggerMessage delegates instead of LoggerExtensions methods for all logging.

**Rationale:**
- CA1848 analyzer requires this pattern
- Significant performance improvement for high-frequency logging
- Structured logging with compile-time safety
- Consistent with existing codebase patterns (e.g., `GoogleJobsApiClient`)

### 6. No Retry on Parser Failures
**Decision:** Do NOT retry on parser failures (structural issues with HTML/JSON).

**Rationale:**
- Parser failures indicate structural changes in target website
- Retrying won't fix the issue - same broken structure will be returned
- Wastes resources and delays detection of the real problem
- Better to fail fast and alert developers to update parsers

### 7. Retry on 429 (Rate Limit) with Standard Backoff
**Decision:** Retry 429 errors with same exponential backoff as 5xx errors (not longer backoff as originally planned).

**Rationale:**
- Simpler implementation - single delay calculation
- Exponential backoff already provides increasing delays
- Jitter adds randomness to prevent synchronized retries
- Can be enhanced later if rate limiting proves insufficient

### 8. Wrap HTTP Requests with Retry Policy
**Decision:** Wrap individual HTTP requests with `_retryPolicy.ExecuteAsync()` rather than wrapping entire search methods.

**Rationale:**
- More granular control - each HTTP call gets its own retry logic
- Allows different retry behavior for different endpoints
- Existing consent page bypass logic in Google Jobs remains intact
- Retry logic is transparent to higher-level code

## Trade-offs Considered

### Polly v7 vs v8
**Chosen:** Polly v7 (existing in codebase)
**Trade-off:** v8 has newer API with `ResiliencePipelineBuilder`, but v7 is stable and widely used

### Custom Retry vs Polly
**Chosen:** Polly
**Trade-off:** Custom retry would be simpler but Polly provides battle-tested patterns

### Configuration vs Hardcoded Values
**Chosen:** Hardcoded retry counts (3 for Google, 4 for Glassdoor)
**Trade-off:** Configuration adds flexibility but increases complexity; can be added later if needed

### Retry on All Exceptions vs Specific Exceptions
**Chosen:** Retry on all HttpRequestException (network errors)
**Trade-off:** More aggressive retry but catches transient network issues; Polly handles this well

## Future Considerations

1. **Configuration:** Add retry settings to platform options classes
2. **Metrics:** Track retry counts and success rates
3. **Circuit Breaker:** Add circuit breaker for persistent failures
4. **Retry-After Header:** Extract delay from Retry-After header when available
5. **Adaptive Retry:** Adjust retry behavior based on historical success rates

