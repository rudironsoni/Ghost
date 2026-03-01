# ADR-0002: Retry Policy with Exponential Backoff

## Status
Accepted (2026-01-31)

## Context
Platform APIs (Indeed, Glassdoor, Google Jobs) exhibit transient failures requiring resilient retry strategies. Need to handle 429 (rate limiting) and 5xx errors gracefully.

## Decision
Implement Polly-based retry policies with:
- Exponential backoff with jitter
- Platform-specific retry counts (3 for Google Jobs, 4 for Glassdoor)
- Separate handling for 429 vs 5xx errors
- Circuit breaker integration for persistent failures

## Alternatives Considered
1. Linear retry with fixed delay - Rejected: doesn't handle rate limiting well
2. Immediate retry - Rejected: increases failure probability
3. No retry (fail fast) - Rejected: too brittle for production

## Consequences
- Positive: Improved success rate on transient failures
- Positive: Respects rate limits through backoff
- Positive: Different strategies per platform
- Negative: Increased latency for failed requests
- Negative: Additional complexity in error handling

## Evidence
- **Documents:**
  - docs/archive/2026/02/02/sisyphus_backup/notepads/retry-implementation/decisions.md
- **Commits:**
  - 009158f - fix(indeed): fix parser salary handling and retry delays
