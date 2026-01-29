# JobSpy Analysis & Ghost Platform Improvements

## Executive Summary

Analysis of JobSpy's job scraping patterns reveals key differences that explain why Ghost's scrapers have varying success rates. JobSpy uses sophisticated HTTP request shaping, browser-like fingerprinting, and robust fallback strategies that Ghost can adopt.

## Platform-Specific Findings

### Glassdoor Implementation Comparison

**JobSpy Patterns:**
- Uses `tls_client` (TLSRotating) for browser-like TLS fingerprinting
- Comprehensive header set including `sec-ch-ua`, `sec-fetch-*`, `origin`, `referer`
- Robust CSRF token extraction with multiple fallback patterns
- Automatic retry/backoff with proxy rotation
- Parallel job detail fetching with throttling

**Ghost Current State:**
- Minimal headers (only `gd-csrf-token`, `apollographql-client-name`)
- No TLS fingerprinting
- Basic HttpClient with optional rotating proxy
- Single-threaded job fetching

**Key Improvements Needed:**
1. Add comprehensive browser headers
2. Implement Polly retry/backoff for 429 responses
3. Add TLS fingerprinting or Playwright fallback
4. Implement parallel job detail fetching with jitter

### Google Jobs Implementation Comparison

**JobSpy Patterns:**
- Correct async pagination parameters: `fc=cursor&fcv=3&async=<bootstrap_string>`
- Full browser headers for both search and async calls
- Persistent CookieContainer for consent handling
- Proper TLS fingerprinting via tls_client

**Ghost Current State:**
- Incorrect async parameter: `_fmt=cursor` (should be `fc=cursor`)
- Minimal headers (only User-Agent)
- No cookie persistence
- Currently disabled due to consent blocking

**Key Improvements Needed:**
1. Fix async parameter structure
2. Add comprehensive headers and CookieContainer
3. Implement proper TLS fingerprinting
4. Add Playwright fallback for consent pages

### Indeed Implementation Comparison

**JobSpy Patterns:**
- Mobile app impersonation with realistic headers
- API key rotation from config/secrets
- Exponential backoff with jitter for 429 responses
- Proxy rotation with health tracking

**Ghost Current State:**
- Hard-coded API key in source code
- Fixed 1-second delay for 429 responses
- Creates new HttpClient per request (socket exhaustion risk)
- No exponential backoff or jitter

**Key Improvements Needed:**
1. Move API key to configuration
2. Implement exponential backoff with Retry-After header support
3. Use HttpClientFactory for connection pooling
4. Add header variability and timing jitter

## Cross-Platform Recommendations

### High Priority (All Platforms)

1. **Header Enrichment**
   - Add comprehensive browser headers: `sec-ch-ua*`, `sec-fetch-*`, `origin`, `referer`
   - Use realistic User-Agent strings
   - Add Accept-Language headers

2. **Retry & Backoff Strategy**
   - Implement Polly for exponential backoff with jitter
   - Handle 429 responses with Retry-After header support
   - Add circuit breaker patterns

3. **Cookie & Session Management**
   - Use persistent CookieContainer across requests
   - Implement session reuse where possible
   - Add cookie clearing/rotation strategies

### Medium Priority

4. **TLS Fingerprinting**
   - Integrate Playwright fallback for hard blocks
   - Consider .NET TLS client libraries
   - Implement automatic fallback when HttpClient fails

5. **Proxy Management**
   - Implement proxy health tracking
   - Add proxy rotation with quarantining
   - Support residential/ISP proxies

6. **Rate Limiting & Timing**
   - Add request jitter between calls
   - Implement concurrent request throttling
   - Respect platform-specific rate limits

### Implementation Priority Order

1. **Glassdoor** - Most critical improvements needed
2. **Google Jobs** - Fix async parameters and re-enable
3. **Indeed** - Security improvements (API key) and backoff

## Next Steps

### Immediate Actions (Next 1-2 hours)
1. **Glassdoor**: Add comprehensive headers and Polly retry
2. **Google Jobs**: Fix async parameters and re-enable scraper
3. **Indeed**: Move API key to configuration

### Short-term (Next day)
1. Implement Playwright fallback for all platforms
2. Add comprehensive testing for anti-scraping scenarios
3. Implement proxy rotation with health checks

### Medium-term (Next week)
1. Add TLS fingerprinting capabilities
2. Implement advanced rate limiting
3. Add monitoring and alerting for scraper health

## Files Requiring Changes

### Glassdoor
- `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorConstants.cs` (headers)
- `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs` (retry logic)
- `src/Platforms/Ghost.Platform.Glassdoor/GlassdoorExtension.cs` (Playwright fallback)

### Google Jobs
- `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs` (async params)
- `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs` (headers, cookies)
- `.env` (re-enable Google extension)

### Indeed
- `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedConstants.cs` (API key config)
- `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs` (HttpClientFactory, backoff)

## Success Metrics

- **Glassdoor**: Reduce 403 error rate from current state to <10%
- **Google Jobs**: Enable scraper and achieve >80% success rate
- **Indeed**: Maintain current success rate while improving security and reliability

This analysis provides a clear roadmap for improving Ghost's job scraping capabilities based on proven JobSpy patterns.