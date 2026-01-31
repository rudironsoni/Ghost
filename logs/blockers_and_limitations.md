# Blockers and Limitations - Job Platforms Fix

## Date: 2026-01-31

## Overview

This document documents the blockers and limitations encountered during the job platforms fix implementation. These blockers prevent certain platforms from working despite implementing all known fixes.

---

## Platform-Specific Blockers

### 1. Google Jobs - Consent Page Blocking

**Status**: BLOCKED

**Problem**: Google Jobs is blocked by sophisticated consent pages that prevent both HTTP and browser-based scraping from working.

**Evidence**:
- HTTP requests return consent page HTML instead of job listings
- Browser fallback detects consent pages and cannot bypass them
- Multiple consent dismissal strategies attempted but all failed
- Logs show: "Detected consent page - no job data available"

**Attempts Made**:
1. ✅ Updated all headers to match JobSpy implementation
2. ✅ Added Google-specific headers (x-browser-channel, x-browser-copyright, x-browser-year)
3. ✅ Added extensive sec-ch-ua headers for browser fingerprinting
4. ✅ Updated User-Agent to Chrome 130 on macOS
5. ✅ Implemented browser fallback with consent page detection
6. ✅ Tried multiple consent dismissal strategies (Reject all, Customize, etc.)

**Root Cause**:
- Google's consent pages are increasingly sophisticated
- Bot detection mechanisms are very advanced
- Consent pages may use CAPTCHA or other anti-automation techniques
- The async parameter (_basejs) from JobSpy may be required but not yet implemented

**Potential Solutions**:
1. Implement async parameter (_basejs) generation from JobSpy
2. Use CAPTCHA solving services (e.g., 2Captcha, Anti-Captcha)
3. Implement more sophisticated consent page bypass techniques
4. Use residential proxies to avoid IP-based blocking
5. Implement cookie persistence across sessions

**Estimated Effort**: High (requires research and implementation)

**Priority**: Medium (Google Jobs is not critical if other platforms work)

---

### 2. Glassdoor - Consent Page Blocking

**Status**: BLOCKED

**Problem**: Glassdoor is blocked by consent pages that prevent both HTTP and browser-based scraping from working.

**Evidence**:
- HTTP requests return consent page HTML instead of job listings
- Browser fallback detects consent pages and cannot bypass them
- Logs show: "Consent page detected, attempting to bypass" followed by "Found 0 jobs via browser"

**Attempts Made**:
1. ✅ Updated all headers to match JobSpy implementation
2. ✅ Added Apollo GraphQL headers (apollographql-client-name, apollographql-client-version)
3. ✅ Added authority, origin, referer headers
4. ✅ Updated User-Agent to Chrome 138 on macOS
5. ✅ Implemented browser fallback with consent page detection
6. ✅ Tried consent dismissal with "Accept" button

**Root Cause**:
- Glassdoor's consent pages are sophisticated
- Bot detection mechanisms are very advanced
- The fallback token from JobSpy may be required but not yet implemented
- GraphQL query structure may need updating to match JobSpy

**Potential Solutions**:
1. Implement fallback token mechanism from JobSpy
2. Update GraphQL query structure to match JobSpy
3. Use CAPTCHA solving services
4. Implement more sophisticated consent page bypass techniques
5. Use residential proxies to avoid IP-based blocking

**Estimated Effort**: High (requires research and implementation)

**Priority**: Medium (Glassdoor is not critical if other platforms work)

---

### 3. InfoJobs - Missing API Credentials

**Status**: BLOCKED (User Action Required)

**Problem**: InfoJobs requires real API credentials (ClientId/ClientSecret) to function. No public or test credentials are available.

**Evidence**:
- HTTP requests return HTTP 500 error with placeholder credentials
- Logs show: "Received HTTP response headers after 37.8567ms - 500"
- GitHub search found no public/test credentials
- Basic Auth implementation is correct but credentials are invalid

**Attempts Made**:
1. ✅ Fixed Basic Auth bug in TecnoempleoApiClient.cs
2. ✅ Searched GitHub for public/test credentials
3. ✅ Documented credential requirements
4. ✅ Updated .env.example with placeholders
5. ✅ Added registration URLs to documentation

**Root Cause**:
- InfoJobs does not provide public or test API credentials
- Credentials must be obtained by registering as a partner
- No viable web scraping fallback exists for InfoJobs

**Required Action**:
- User must register at https://www.infojobs.net/empresas
- User must obtain ClientId and ClientSecret from InfoJobs
- User must configure credentials in .env file

**Estimated Effort**: Low (user action, not technical)

**Priority**: High (InfoJobs is a major Spanish job platform)

---

### 4. Tecnoempleo - Missing API Credentials

**Status**: BLOCKED (User Action Required)

**Problem**: Tecnoempleo requires real API credentials (ClientId/ClientSecret) to function. No public or test credentials are available.

**Evidence**:
- HTTP requests fail with placeholder credentials
- GitHub search found no public/test credentials
- Basic Auth implementation is correct but credentials are invalid

**Attempts Made**:
1. ✅ Fixed Basic Auth bug in TecnoempleoApiClient.cs
2. ✅ Searched GitHub for public/test credentials
3. ✅ Documented credential requirements
4. ✅ Updated .env.example with placeholders
5. ✅ Added registration URLs to documentation

**Root Cause**:
- Tecnoempleo does not provide public or test API credentials
- Credentials must be obtained by contacting the platform
- No viable web scraping fallback exists for Tecnoempleo

**Required Action**:
- User must contact Tecnoempleo at https://www.tecnoempleo.com/
- User must request API access and obtain ClientId/ClientSecret
- User must configure credentials in .env file

**Estimated Effort**: Low (user action, not technical)

**Priority**: High (Tecnoempleo is a major Spanish job platform)

---

## Summary of Blockers

| Platform | Blocker Type | Status | Required Action |
|----------|--------------|--------|-----------------|
| Google | Technical (Consent Pages) | BLOCKED | Implement async parameter, CAPTCHA solving |
| Glassdoor | Technical (Consent Pages) | BLOCKED | Implement fallback token, update GraphQL |
| InfoJobs | User Action (Credentials) | BLOCKED | User must obtain API credentials |
| Tecnoempleo | User Action (Credentials) | BLOCKED | User must obtain API credentials |

---

## Technical Blockers vs User Action Blockers

### Technical Blockers (Google, Glassdoor)
- **Nature**: Require additional research and implementation
- **Complexity**: High
- **Timeline**: Unknown (requires investigation)
- **Ownership**: Development team

### User Action Blockers (InfoJobs, Tecnoempleo)
- **Nature**: Require user to obtain credentials from platforms
- **Complexity**: Low
- **Timeline**: Depends on user
- **Ownership**: User

---

## Recommendations

### For Technical Blockers (Google, Glassdoor)

1. **Short-term**:
   - Document the blockers clearly
   - Mark platforms as "requires additional work"
   - Focus on platforms that are working (LinkedIn, Indeed)

2. **Medium-term**:
   - Research async parameter implementation for Google
   - Research fallback token implementation for Glassdoor
   - Evaluate CAPTCHA solving services

3. **Long-term**:
   - Implement sophisticated consent page bypass techniques
   - Consider using residential proxies
   - Monitor platform changes and update scrapers accordingly

### For User Action Blockers (InfoJobs, Tecnoempleo)

1. **Immediate**:
   - Provide clear documentation on how to obtain credentials
   - Update .env.example with placeholders
   - Add registration URLs to documentation

2. **Follow-up**:
   - Assist user with credential configuration if needed
   - Test platforms once credentials are obtained
   - Update documentation with any issues encountered

---

## Conclusion

The job platforms fix implementation has made significant progress:

**Successfully Fixed**:
- ✅ LinkedIn: Working (was already working)
- ✅ Indeed: Fixed and working (Content-Type header + parser fix)

**Blocked**:
- ❌ Google: Consent pages blocking (technical blocker)
- ❌ Glassdoor: Consent pages blocking (technical blocker)
- ❌ InfoJobs: Missing credentials (user action blocker)
- ❌ Tecnoempleo: Missing credentials (user action blocker)

**Success Rate**: 2 out of 6 platforms working (33%)

The technical blockers (Google, Glassdoor) require additional research and implementation. The user action blockers (InfoJobs, Tecnoempleo) require the user to obtain API credentials from the platforms.

---

## References

- JobSpy GitHub: https://github.com/speedyapply/JobSpy
- Google Constants: https://github.com/speedyapply/JobSpy/blob/main/jobspy/google/constant.py
- Glassdoor Constants: https://github.com/speedyapply/JobSpy/blob/main/jobspy/glassdoor/constant.py
- Credential Requirements: `logs/credential_requirements.md`
- JobSpy Analysis: `logs/jobspy_vs_ghost_analysis.md`
