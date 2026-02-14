# Ghost Smoke Tests

Real data smoke tests for Ghost job platform scrapers.

## Quick Start

```bash
# Set environment variables (optional - defaults provided)
export GHOST_BASE_URL="https://localhost:5001"
export GHOST_API_KEY="your-api-key"

# Run all smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj

# Run only LinkedIn tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "FullyQualifiedName~LinkedIn"

# Run only end-to-end flow tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke&Flow=EndToEnd"

# Run with detailed output
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed"
```

## Purpose

These tests validate that scrapers return real, fresh job data from production endpoints. They run manually against a live Ghost instance with zero mocking.

## Structure

- **Assertions/** - Data quality assertion library
  - `JobDataQualityAssertions.cs` - Extension methods for validating job data
  - `JobDataQualityAssertionsTests.cs` - Tests for assertion library

- **Platforms/** - Platform-specific smoke tests
  - `PlatformSmokeTestFixture.cs` - Shared fixture with service provider
  - `LinkedInSmokeTests.cs` - LinkedIn platform tests
  - `IndeedSmokeTests.cs` - Indeed platform tests
  - `GlassdoorSmokeTests.cs` - Glassdoor platform tests
  - `InfoJobsSmokeTests.cs` - InfoJobs platform tests
  - `GoogleSmokeTests.cs` - Google platform tests

- **Flows/** - End-to-end workflow tests
  - `EndToEndSmokeTests.cs` - Complete user journey tests
  - `MultiPlatformAggregationTests.cs` - Cross-platform aggregation tests

## Test Traits

Tests are organized by traits for selective execution:

- **Category**: `Smoke` - All smoke tests
- **Platform**: `LinkedIn`, `Indeed`, `Glassdoor`, `InfoJobs`, `Google` - Platform-specific tests
- **Flow**: `EndToEnd`, `MultiPlatform` - Workflow tests

## Configuration

Environment variables (optional - defaults provided):

| Variable | Default | Description |
|----------|---------|-------------|
| `GHOST_BASE_URL` | `https://localhost:5001` | Ghost API base URL |
| `GHOST_API_KEY` | (none) | API key for authentication |

## Execution Notes

- Tests connect to real production endpoints
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
