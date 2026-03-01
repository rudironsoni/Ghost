# Google Jobs integration test learnings

- Test executed: 2026-02-01T08:13:41+01:00
- Result: Server not running locally; tests skipped

Findings:

- Local development server at http://localhost:5000/api/jobs/search did not respond within short connectivity check (connect-timeout 2s, max-time 5s).
- logs/integration_test_google.md created with documented skip.

Next steps:

1. Start the web API (dotnet run or host) locally and re-run the integration test.
2. Ensure health endpoint /api/jobs/health returns 200 before running the 20 requests.

## SessionOrchestrator Integration (Task Completed)

### Implementation Details
- Added dual constructor pattern to `GoogleJobsApiClient` following `IndeedApiClient` and `GlassdoorApiClient` patterns
- Legacy constructor: `GoogleJobsApiClient(HttpClient, GoogleJobsOptions, ILogger)` - maintains backward compatibility
- Modern constructor: `GoogleJobsApiClient(ISessionOrchestrator, GoogleJobsOptions, ILogger)` - enables session management
- Split `SearchAsync` into `SearchWithOrchestratorAsync` and `SearchLegacyAsync` for clean separation
- Extracted common logic into `ExecuteSearchAsync(query, location, httpSession?)` to avoid duplication

### Session Management Features
- Session allocation with affinity key: `google_jobs_{query}_{location}_{guid}` for pagination consistency
- Session health monitoring: `CheckAndRecycleSessionAsync()` method to handle unhealthy sessions
- Proper resource cleanup: `IDisposable` implementation with session closure in `Dispose()`
- Session affinity duration: 5 minutes to maintain same session for paginated requests
- Complexity score: 40 (similar to Glassdoor's 50, lower than Indeed's 30)

### Key Integration Points
1. Initial request uses orchestrator-allocated HTTP session
2. Pagination requests (async callbacks) use same session for consistency
3. Retry requests during consent bypass use same session
4. Session closed in finally block to ensure cleanup
5. Health check before recycling ensures unhealthy sessions are replaced

### Project Dependencies
- Added `Ghost.Platform.Common` project reference to access:
  - `ISessionOrchestrator` interface
  - `SessionAllocationContext` record
  - `SessionAffinityOptions` record
  - `SessionHealthMetrics` record
  - `RotatingProxySession` class
  - `SessionType` and `SessionHealth` enums

### Verification
- Build successful: `dotnet build src/Platforms/Ghost.Platform.Google/Ghost.Platform.Google.csproj`
- All backward compatibility maintained through dual constructor pattern
- No breaking changes to existing public APIs
