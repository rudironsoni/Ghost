# Fix Job Platforms (Google, Glassdoor, Indeed)

## Objective
Analyze, debug, and fix the job fetching logic for **Google Jobs**, **Glassdoor**, and **Indeed** in the `Ghost` solution. All three are currently returning 0 results.

## Context & Findings
- **Google Jobs**:
  - Current: Uses `ibp=htl;jobs`.
  - Fix Candidate: Reference `JobSpy` uses `udm=8` parameter. The HTML structure for `data-async-fc` (cursor) might have changed, breaking the regex.
- **Glassdoor**:
  - Current: Scrapes CSRF token from homepage, then hits GraphQL.
  - Fix Candidate: The CSRF regex `\"token\"\s*:\s*\"(?<t>[^\"]+)\"` may be outdated. The User-Agent might be blocked.
- **Indeed**:
  - Current: Impersonates iPhone App (`Indeed App 193.1`) with a hardcoded API key.
  - Fix Candidate: API Key might be revoked. `JobSpy` disables SSL verification (`verify=False`) to bypass TLS fingerprinting; `Ghost` tries to handle this via `SslProtocols`, but it might be insufficient.

## Task 1: Create Reproduction & Debugging Tools
**Goal**: Create standalone C# console apps or integration tests to run the scrapers and dump the *raw* responses (HTML/JSON) to disk. We need to see *what* Google/Indeed are actually sending back (Captcha? Changed HTML? Empty JSON?).

1.  **Create `tests/DebugScraper/` (New Console App)**:
    - Add references to `Ghost.Platform.*`.
    - Create a simple `Program.cs` that:
        - Instantiates `GoogleJobClient`, `IndeedJobClient`, `GlassdoorJobClient`.
        - Runs a search (e.g., "Software Engineer" in "Remote").
        - **CRITICAL**: Modifies the internal `ILogger` to write the **full raw HTML/JSON body** to a file (e.g., `google_response.html`, `indeed_response.json`). Currently, the code logs length but maybe not the full body on failure.
    - **Run it**: Confirm it returns 0 results and inspect the saved files.

## Task 2: Google Jobs Fix Iterations
1.  **Analyze `google_response.html`**:
    - Does it contain "Verify you are human"? -> **Bot Detection**.
    - Does it contain job listings but the regex failed? -> **Regex/Parser Fix**.
    - Is it a generic Google Search page? -> **Parameter Fix**.
2.  **Apply Fixes**:
    - **Param**: Change URL to use `udm=8` instead of/in addition to `ibp=htl;jobs`.
    - **Regex**: If HTML has data but regex fails, update `GoogleJobsConstants.DataAsyncFcRegex` and `GoogleJobsParser` logic.
    - **Headers**: Rotate User-Agent to match latest Chrome (130+).

## Task 3: Indeed Fix Iterations
1.  **Analyze `indeed_response.json`**:
    - Is it 403/429? -> **API Key/TLS issue**.
    - Is it 200 OK but empty `results`? -> **Query Format issue**.
2.  **Apply Fixes**:
    - **API Key**: Check `JobSpy` repo or online for a newer "Indeed App" API key.
    - **TLS**: Experiment with `HttpClientHandler` settings in `IndeedApiClient.cs`.
    - **Query**: Verify the GraphQL query structure matches *exactly* what the mobile app expects.

## Task 4: Glassdoor Fix Iterations
1.  **Analyze `glassdoor_response.html` (CSRF fetch)**:
    - Can we find the "token": "..." string?
    - If not, update `GlassdoorApiClient.cs` regex.
2.  **Analyze GraphQL Response**:
    - If CSRF works but Search returns 0, check the GraphQL variables and `apollographql-client-version` headers.

## Execution Plan (Ralph Loop)
Run the following prompt in a Ralph Loop to execute this:

```markdown
/ralph-loop
**Mission**: Fix Google, Glassdoor, and Indeed scrapers returning 0 results.

**Phase 1: Diagnosis (The "Why")**
1. Create a `DebugScraper` console app in `tests/DebugScraper` to run all 3 clients.
2. MODIFY the `*ApiClient.cs` files temporarily to log/save the **RAW HTML/JSON RESPONSE BODIES** to disk (e.g., `logs/google.html`).
3. RUN the debugger.
4. ANALYZE the output files:
   - **Google**: Open `google.html`. Do you see specific job cards? If yes, the Parser regex is broken. If no (captcha/wrong page), the URL/headers are broken.
   - **Indeed**: Open `indeed.json`. Is it an error message?
   - **Glassdoor**: Did we get a CSRF token?

**Phase 2: Fix Implementation**
- **Google**:
  - Try adding `&udm=8` to the search URL.
  - Update `GoogleJobsConstants` with latest Chrome User-Agent.
  - Fix Regex in `GoogleJobsParser.cs` based on the `google.html` content.
- **Indeed**:
  - Update `IndeedConstants` API Key (search GitHub/JobSpy for latest).
  - Verify TLS handshake settings in `IndeedApiClient`.
- **Glassdoor**:
  - Update CSRF extraction regex in `GlassdoorApiClient`.

**Phase 3: Verification**
- Run `DebugScraper` again.
- Assert > 0 results found for "Software Engineer" in "New York".
- Revert temporary logging changes (clean up code).
```
