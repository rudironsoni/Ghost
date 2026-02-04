# Ghost Platform Verification and Fix Plan

## TL;DR

> **Quick Summary**: Fix WebAPI endpoint registration issue and verify Google Jobs/Glassdoor platform functionality
> 
> **Deliverables**: 
> - Fixed WebAPI endpoint registration  
> - Verified Google Jobs platform functionality (>0 jobs returned)
> - Verified Glassdoor platform functionality (>0 jobs returned)
> - Debug log analysis and findings report
> 
> **Estimated Effort**: Short (2-3 hours)
> **Parallel Execution**: NO - sequential debugging required
> **Critical Path**: Fix Endpoints → Test Google Jobs → Test Glassdoor → Check Logs → Report

---

## Context

### Original Issue
User initially asked to "fix Google Jobs and Glassdoor" assuming they were broken, but investigation revealed:
- Both platforms have sophisticated implementations with comprehensive anti-detection measures
- Google Jobs: Consent bypass, rotating proxies, multiple retry strategies
- Glassdoor: CSRF token handling, location fixes, enhanced parsing
- **The real issue**: WebAPI endpoint registration error preventing proper testing

### Current State Assessment
- ✅ LinkedIn, Indeed: Working and enabled
- ✅ InfoJobs: Implemented (needs real credentials)
- 🔶 Google Jobs: Sophisticated implementation (UNKNOWN functionality)
- 🔶 Glassdoor: Robust implementation (UNKNOWN functionality)
- ❌ WebAPI: Endpoint registration error preventing testing

---

## Work Objectives

### Core Objective
Fix the WebAPI endpoint registration issue and verify if Google Jobs and Glassdoor platforms are actually working (returning >0 jobs)

### Definition of Done
- [ ] WebAPI endpoints register without errors
- [ ] Google Jobs returns >0 jobs when tested
- [ ] Glassdoor returns >0 jobs when tested  
- [ ] Debug logs analyzed and insights documented
- [ ] Clear verdict: "Working" vs "Broken" vs "Partially Working" for each platform

### Must Have
- Fixed endpoint registration that allows HTTP POST requests
- Actual test results (not just technical fixes)
- Debug log examination for troubleshooting insights

### Must NOT Have
- Assumptions about functionality without testing
- "Black box" - need to see actual job results or specific error patterns

---

## Verification Strategy

### Test Infrastructure  
- **User wants tests**: YES - actual functional verification required
- **Framework**: Manual testing with curl commands
- **QA approach**: Automated API calls + debug log analysis

### Test Commands
Each platform will be tested with standardized curl commands:

```bash
# Test Google Jobs
curl -X POST http://localhost:5000/api/jobs/search \
  -H "Content-Type: application/json" \
  -d '{"Query": "Software Engineer", "Location": "Remote", "MaxResults": 5, "Sources": ["Google"]}'

# Test Glassdoor  
curl -X POST http://localhost:5000/api/jobs/search \
  -H "Content-Type: application/json" \
  -d '{"Query": "Data Engineer", "Location": "Remote", "MaxResults": 5, "Sources": ["Glassdoor"]}'

# Check debug logs
ls -la logs/
cat logs/google_jobs_search.html
cat logs/glassdoor_search_*.json
```

### Success Criteria per Platform
- **Google Jobs**: 
  - ✅ Returns HTTP 200 with non-empty job results
  - ✅ Jobs array contains >0 entries
  - ✅ Debug logs show successful parsing
- **Glassdoor**:
  - ✅ Returns HTTP 200 with non-empty job results  
  - ✅ Jobs array contains >0 entries
  - ✅ Debug logs show successful CSRF token extraction

---

## Execution Strategy

### Sequential Execution Required
This is sequential debugging - each step depends on the previous one's results.

```
Phase 1: Fix Endpoint Registration
↓ (must complete before testing)
Phase 2: Test Google Jobs Platform  
↓ (results inform next steps)
Phase 3: Test Glassdoor Platform
↓ (results inform next steps)
Phase 4: Analyze Debug Logs
↓ (final verification)
Phase 5: Generate Results Report
```

---

## TODOs

### Phase 1: Fix WebAPI Endpoint Registration

- [ ] 1. Fix ASP.NET Core endpoint parameter binding issue

  **What to do**:
  - Add explicit `[FromServices]` attribute to IJobClient parameters in JobsEndpoints.cs
  - This prevents ASP.NET Core from incorrectly inferring body parameters
  
  **Must NOT do**:
  - Don't change parameter types or method signatures
  - Don't modify JobSearchCriteria class
  - Don't change endpoint URLs or HTTP methods

  **Recommended Agent Profile**:
  > Select category + skills based on task domain. Justify each choice.
  - **Category**: `quick`
    - Reason: Simple parameter binding fix, single file change
  - **Skills**: [`git-master`]
    - `git-master`: For atomic commit after fix
  - **Skills Evaluated but Omitted**:
    - `frontend-ui-ux`: Not UI work
    - `playwright`: Not browser automation

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: All subsequent testing (Tasks 2, 3, 4, 5)
  - **Blocked By**: None (can start immediately)

  **References**:
  - `src/Ghost.WebApi/Features/Jobs/JobsEndpoints.cs` - File to fix
  - ASP.NET Core minimal API parameter binding documentation

  **Acceptance Criteria**:
  - [ ] WebAPI builds without errors
  - [ ] WebAPI starts without runtime exceptions
  - [ ] `/api/jobs/search` endpoint responds to HTTP POST requests
  - [ ] No "Body was inferred" errors in application logs

### Phase 2: Test Google Jobs Platform Functionality

- [ ] 2. Test Google Jobs platform with real search requests

  **What to do**:
  - Start WebAPI application
  - Make HTTP POST requests to `/api/jobs/search` with Google-specific parameters
  - Verify response structure and job results
  - Enable debug mode to capture HTML/JSON output
  
  **Must NOT do**:
  - Don't modify Google Jobs implementation code
  - Don't change platform configuration
  - Don't disable or modify other platforms (LinkedIn, Indeed)

  **Recommended Agent Profile**:
  > Select category + skills based on task domain. Justify each choice.
  - **Category**: `quick`
    - Reason: Manual testing and verification, straightforward process
  - **Skills**: [`playwright`]
    - `playwright`: Not needed for API testing, overkill for this task
  
  **Skills Evaluated but Omitted**:
  - `playwright`: Manual HTTP testing is simpler for this
  - `frontend-ui-ux`: Not UI work

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: Platform comparison (Task 3) 
  - **Blocked By**: Task 1 must complete first

  **References**:
  - `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs` - Implementation to verify
  - README.md - Google Jobs configuration and troubleshooting

  **Acceptance Criteria**:
  - [ ] HTTP POST to `/api/jobs/search` returns HTTP 200
  - [ ] Response contains jobs array with >0 entries
  - [ ] Response includes platform success metadata
  - [ ] Debug logs show successful parsing and no consent page blocks

### Phase 3: Test Glassdoor Platform Functionality  

- [ ] 3. Test Glassdoor platform with real search requests

  **What to do**:
  - Continue with same WebAPI instance  
  - Make HTTP POST requests to `/api/jobs/search` with Glassdoor-specific parameters
  - Verify response structure and job results
  - Check debug logs for CSRF token extraction and location handling
  
  **Must NOT do**:
  - Don't modify Glassdoor implementation code
  - Don't disable or modify other platforms
  - Don't change Glassdoor configuration

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Same as Task 2, sequential testing process
  - **Skills**: [none required]
    - Standard HTTP testing is sufficient
  
  **Parallelization**:
  - **Can Run In Parallel**: NO  
  - **Parallel Group**: Sequential
  - **Blocks**: Debug log analysis (Task 4)
  - **Blocked By**: Tasks 1 and 2 must complete first

  **References**:
  - `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs` - Implementation to verify
  - README.md - Glassdoor configuration and troubleshooting

  **Acceptance Criteria**:
  - [ ] HTTP POST to `/api/jobs/search` returns HTTP 200  
  - [ ] Response contains jobs array with >0 entries
  - [ ] Response includes platform success metadata
  - [ ] Debug logs show successful CSRF token extraction and location resolution

### Phase 4: Debug Log Analysis

- [ ] 4. Examine debug logs for troubleshooting insights

  **What to do**:
  - Check `logs/` directory for debug output files
  - Analyze HTML files from Google Jobs searches  
  - Analyze JSON files from Glassdoor searches
  - Look for patterns in successful vs failed attempts
  - Document key insights about platform behavior
  
  **Must NOT do**:
  - Don't modify debug log files
  - Don't delete or clean logs during analysis
  - Don't make assumptions without evidence

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: File examination and analysis, straightforward process
  - **Skills**: [none required]
    - Standard file reading and analysis is sufficient

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential  
  - **Blocks**: Final report (Task 5)
  - **Blocked By**: Tasks 2 and 3 must complete first

  **References**:
  - `logs/google_jobs_search.html` - Google Jobs debug output (if exists)
  - `logs/glassdoor_search_*.json` - Glassdoor debug output (if exists)
  - README.md - Debug mode documentation

  **Acceptance Criteria**:
  - [ ] All debug log files documented and analyzed
  - [ ] Key insights about platform behavior identified
  - [ ] Success/failure patterns documented
  - [ ] Technical details captured for troubleshooting

### Phase 5: Generate Final Results Report

- [ ] 5. Create comprehensive results report

  **What to do**:
  - Compile test results from Tasks 2 and 3
  - Document findings from debug log analysis (Task 4)  
  - Provide clear verdict for each platform: Working/Broken/Partially Working
  - Include specific technical details and recommendations
  
  **Must NOT do**:
  - Don't make assumptions about functionality
  - Don't include speculation without evidence
  - Don't provide generic recommendations

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Technical documentation and reporting
  - **Skills**: [`git-master`]
    - `git-master`: For reviewing and committing final report

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: None (final task)
  - **Blocked By**: All previous tasks must complete

  **References**:
  - Previous task results and debug log analysis
  - README.md for context and recommendations

  **Acceptance Criteria**:
  - [ ] Clear verdict for each platform with evidence
  - [ ] Technical details about working vs broken functionality
  - [ ] Specific recommendations based on findings
  - [ ] Complete audit trail of testing process

---

## Success Criteria

### Final Verification Commands
```bash
# Should all return HTTP 200 with job results
curl -X POST http://localhost:5000/api/jobs/search \
  -H "Content-Type: application/json" \
  -d '{"Query": "Software Engineer", "Location": "Remote", "MaxResults": 5, "Sources": ["Google"]}'

curl -X POST http://localhost:5000/api/jobs/search \
  -H "Content-Type: application/json" \
  -d '{"Query": "Data Engineer", "Location": "Remote", "MaxResults": 5, "Sources": ["Glassdoor"]}'

# Should show debug output files
ls -la logs/
```

### Final Checklist
- [ ] WebAPI endpoint registration fixed (no "Body was inferred" errors)
- [ ] Google Jobs tested and functionality status determined
- [ ] Glassdoor tested and functionality status determined  
- [ ] Debug logs analyzed for insights
- [ ] Final report with clear verdicts and recommendations generated