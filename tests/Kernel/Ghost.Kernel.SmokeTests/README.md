# Ghost Smoke Tests

Real data smoke tests for Ghost job platform scrapers.

## Quick Start

```bash
# Set environment variables for smoke tests (optional - defaults provided)
export GHOST_SMOKE_BASE_URL="http://localhost:8080"
export GHOST_ADMIN_API_KEY="your-admin-api-key"

# Run all smoke tests (HTTP-based)
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke"

# Run all integration tests (in-process)
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration"

# Run only LinkedIn smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke&Platform=LinkedIn"

# Run only end-to-end flow tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke&Flow=EndToEnd"

# Run with detailed output
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed"
```

## Purpose

Ghost uses a **hybrid test architecture**:

### Integration Tests (In-Process)
- Test platform clients directly through dependency injection
- Run in-process without HTTP overhead
- Require kernel/platform clients to be available
- Faster execution, better debugging

### Smoke Tests (HTTP-Based)
- Test full API stack including HTTP endpoints
- Run against a running Ghost instance
- Validate end-to-end functionality
- More realistic testing environment

## Structure

- **Assertions/** - Data quality assertion library
  - `JobDataQualityAssertions.cs` - Extension methods for validating job data
  - `JobDataQualityAssertionsTests.cs` - Tests for assertion library

- **Integration/** - In-process integration tests
  - `PlatformIntegrationTestFixture.cs` - Shared fixture with service provider
  - `LinkedInIntegrationTests.cs` - LinkedIn platform tests
  - `IndeedIntegrationTests.cs` - Indeed platform tests
  - `GlassdoorIntegrationTests.cs` - Glassdoor platform tests
  - `InfoJobsIntegrationTests.cs` - InfoJobs platform tests
  - `GoogleIntegrationTests.cs` - Google platform tests

- **Smoke/** - HTTP-based smoke tests
  - `HttpSmokeTestFixture.cs` - Shared fixture with HttpClient
  - `GoogleHttpSmokeTests.cs` - Google HTTP tests
  - `LinkedInHttpSmokeTests.cs` - LinkedIn HTTP tests
  - `IndeedHttpSmokeTests.cs` - Indeed HTTP tests
  - `GlassdoorHttpSmokeTests.cs` - Glassdoor HTTP tests
  - `InfoJobsHttpSmokeTests.cs` - InfoJobs HTTP tests
  - `MultiPlatformHttpSmokeTests.cs` - Cross-platform aggregation tests

- **Flows/** - End-to-end workflow tests
  - `EndToEndIntegrationTests.cs` - Complete user journey tests
  - `MultiPlatformAggregationTests.cs` - Cross-platform aggregation tests

## Test Traits

Tests are organized by traits for selective execution:

- **Category**: `Integration` or `Smoke` - Test type
- **Platform**: `LinkedIn`, `Indeed`, `Glassdoor`, `InfoJobs`, `Google` - Platform-specific tests
- **Flow**: `EndToEnd`, `MultiPlatform` - Workflow tests

## Configuration

### Integration Tests
Integration tests use `appsettings.json` for configuration.

### Smoke Tests
Environment variables (optional - defaults provided):

| Variable | Default | Description |
|----------|---------|-------------|
| `GHOST_SMOKE_BASE_URL` | `http://localhost:8080` | Ghost API base URL for smoke tests |
| `GHOST_ADMIN_API_KEY` | (none) | Admin API key for authentication |

## Execution Notes

### Integration Tests
- Run in-process with direct DI
- Test platform clients directly
- Require kernel/platform clients
- Faster execution, better debugging
- Can run in CI (if kernel available)

### Smoke Tests
- Connect to real production endpoints
- No mocking or test doubles
- Validates actual data quality and freshness
- Requires network connectivity to production
- **Manual-only**: Do not run in CI pipelines

## Interpreting Results

### Success Criteria
- All platforms return non-empty results
- Required fields are present (Id, Title, Company, Url, Source)
- Data is fresh (posted within 90 days)
- No duplicate jobs in results
- URLs are reachable

### Common Failures

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| Connection refused | Ghost not running | Start Ghost instance |
| Empty results | Scrapers broken or sites changed | Check scraper logs, update parsers |
| Missing fields | Parser needs updating | Update field extraction logic |
| Stale data | Scraping not running | Check scraping frequency |
| Duplicates | Deduplication issue | Check deduplication logic |

## Sample Output

```
Found 10 jobs

=== Sample Job Data ===
ID: linkedin-1234567890
Title: Senior Software Engineer
Company: Tech Corp
Location: Remote
URL: https://linkedin.com/jobs/view/1234567890
Posted: 2025-12-15
Source: LinkedIn
```

## For More Information

See the comprehensive guide: [docs/testing/smoke-tests.md](../../../docs/testing/smoke-tests.md)
