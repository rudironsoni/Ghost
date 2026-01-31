# Decisions - Retry Implementation

## Task 4.1: Add Retry with Exponential Backoff

### Architectural Decisions

1. **Retry Policy Configuration**
   - **GoogleJobsApiClient**: maxRetries: 3 (existing)
   - **GlassdoorApiClient**: maxRetries: 4 (new)
   - **Rationale**: Glassdoor may have stricter rate limits, so one extra retry provides better resilience

2. **Jitter Enabled**
   - **Decision**: enableJitter: true for both platforms
   - **Rationale**: Prevents thundering herd effect when multiple requests fail simultaneously

3. **Rate Limiting Separation**
   - **Decision**: Kept rate limiting separate from retry policy
   - **Rationale**: Rate limiting is a cross-cutting concern that applies before any request, while retry policy handles transient failures after the request

4. **Logger Parameter Optional**
   - **Decision**: Made ILogger parameter optional in GlassdoorApiClient constructor
   - **Rationale**: Maintains backward compatibility with existing code that may not provide a logger

5. **HttpRequestMessage Creation**
   - **Decision**: Create new HttpRequestMessage for each retry attempt inside the lambda
   - **Rationale**: HttpRequestMessage cannot be reused; this is a subtle but critical API constraint

6. **Error Handling Strategy**
   - **Decision**: Let EnhancedRetryPolicy handle transient errors (429, 5xx, timeouts)
   - **Rationale**: Centralized retry logic is more maintainable than manual retry loops
   - **Preserved**: GraphQL error parsing for non-retryable errors (auth failures, etc.)

7. **Logging Performance**
   - **Decision**: Use LoggerMessage delegates instead of direct logger calls
   - **Rationale**: CA1848 compliance and better performance for high-frequency logging
