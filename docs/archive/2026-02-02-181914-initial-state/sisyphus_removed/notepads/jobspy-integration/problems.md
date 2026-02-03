# Problems - JobSpy Integration

## Task 5: Google Jobs Implementation Fixes

### Unresolved Issues

1. **Widget Key Auto-Discovery**:
   - **Problem**: Widget key "520084652" is hard-coded and may change
   - **Impact**: If Google changes the widget key, scraping will fail
   - **Potential Solution**: Implement auto-discovery by searching for common patterns
   - **Status**: Not implemented in this task, deferred to future work

2. **Bootstrap String Auto-Update**:
   - **Problem**: Async bootstrap string may expire or change
   - **Impact**: Pagination will fail if bootstrap string is outdated
   - **Potential Solution**: Fetch bootstrap string from initial page response
   - **Status**: Made configurable, but auto-update not implemented

3. **Rate Limiting Detection**:
   - **Problem**: No explicit detection of rate limiting
   - **Impact**: May get blocked without knowing why
   - **Potential Solution**: Detect 429 responses and implement exponential backoff
   - **Status**: Not implemented in this task

4. **Consent Page Bypass Reliability**:
   - **Problem**: Consent page bypass relies on alternative URLs and proxies
   - **Impact**: May not always work, especially for strict regions
   - **Potential Solution**: Implement more sophisticated bypass techniques
   - **Status**: Existing implementation is good enough for now

### Technical Debt

1. **Index-Based Field Extraction**:
   - **Debt**: Parser still uses index-based field extraction as fallback
   - **Impact**: Fragile to Google structure changes
   - **Plan**: Gradually migrate to named field extraction
   - **Priority**: Medium

2. **Hard-coded Headers**:
   - **Debt**: Headers are hard-coded in constants
   - **Impact**: May need updates when Google changes requirements
   - **Plan**: Make headers configurable
   - **Priority**: Low

3. **Proxy List Hard-coded**:
   - **Debt**: Proxy list is hard-coded in GoogleJobsApiClient
   - **Debt**: Proxies may be unreliable or go offline
   - **Plan**: Use proxy service or configuration
   - **Priority**: Low

### Known Limitations

1. **No CAPTCHA Handling**:
   - **Limitation**: Cannot handle CAPTCHA challenges
   - **Impact**: Will fail if Google presents CAPTCHA
   - **Workaround**: Use different IP, wait, or use CAPTCHA solving service

2. **Limited Regional Support**:
   - **Limitation**: Primarily tested with US region
   - **Impact**: May not work well for other regions
   - **Workaround**: Test and adjust for specific regions

3. **No Session Persistence Across Runs**:
   - **Limitation**: Cookies are not persisted to disk
   - **Impact**: Each run starts with fresh session
   - **Workaround**: Implement cookie persistence if needed
