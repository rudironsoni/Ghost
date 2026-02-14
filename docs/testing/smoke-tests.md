# Smoke Tests - Comprehensive Guide

## Overview

Ghost uses a **hybrid test architecture** with two types of tests:

### 1. Integration Tests (In-Process)
- **Location**: `tests/Smoke/Ghost.Smoke.Tests/Integration/`
- **Execution**: In-process, direct dependency injection
- **Requirement**: Requires kernel/platform clients to be available
- **Trait**: `[Trait("Category", "Integration")]`
- **Purpose**: Test platform clients directly without HTTP layer

### 2. Smoke Tests (HTTP-Based)
- **Location**: `tests/Smoke/Ghost.Smoke.Tests/Smoke/`
- **Execution**: HTTP requests against running Ghost instance
- **Requirement**: Requires Ghost instance to be running
- **Trait**: `[Trait("Category", "Smoke")]`
- **Purpose**: Test full API stack including HTTP endpoints

## Integration Tests

Integration tests validate that platform clients work correctly by calling them directly through dependency injection. These tests:

- **Run in-process** - Direct DI, no HTTP overhead
- **Test platform clients** - Validate scraper logic directly
- **Require kernel** - Need platform clients to be available
- **Faster execution** - No network latency
- **Better debugging** - Direct access to client internals

### When to Run Integration Tests

- During development of platform clients
- When updating scraper logic
- When adding new platforms
- In CI/CD pipelines (if kernel is available)

### Running Integration Tests

```bash
# Run all integration tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration"

# Run specific platform integration tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration&Platform=LinkedIn"
```

## Smoke Tests

Smoke tests validate that the Ghost API endpoints return fresh, complete, and accurate job data. These tests:

- **Connect to real production endpoints** - No mocks, no test doubles
- **Validate actual data quality** - Check for required fields, freshness, duplicates
- **Run manually** - Not suitable for CI/CD pipelines
- **Provide immediate feedback** - Show sample data for human verification

### Why Smoke Tests Exist

1. **Early Detection**: Catch scraper issues before they affect users
2. **Data Quality**: Ensure scrapers return complete, accurate data
3. **Freshness**: Verify data is recent (within 90 days)
4. **Platform Health**: Monitor each job platform's scraper status
5. **Regression Prevention**: Detect when site changes break scrapers

### What Smoke Tests Validate

- **Non-empty results**: At least one job returned
- **Required fields**: Id, Title, Company, Url, Source present
- **Fresh data**: Jobs posted within 90 days
- **No duplicates**: Unique job IDs in results
- **Reachable URLs**: Job URLs are valid and accessible
- **Valid platform IDs**: Platform-specific ID format correct

## Prerequisites

### For Integration Tests

Integration tests require:
- **Platform clients** - Kernel and platform plugins must be available
- **.NET SDK** - .NET 10.0 or later
- **Configuration** - `appsettings.json` with platform credentials (if needed)

### For Smoke Tests

Before running smoke tests, ensure you have:

#### 1. Ghost Instance Running

```bash
# Check if Ghost is running
curl http://localhost:8080/health

# Expected response:
# {"status":"healthy"}
```

If Ghost is not running, start it:

```bash
# Using dotnet run
cd src/Hosting/Ghost.Hosting
dotnet run

# Or using Docker
docker-compose up -d
```

#### 2. Admin API Key (Optional)

Some endpoints may require authentication. Set the API key:

```bash
export GHOST_ADMIN_API_KEY="your-admin-api-key"
```

#### 3. Network Access

Smoke tests require outbound internet access to:
- LinkedIn (linkedin.com)
- Indeed (indeed.com)
- Glassdoor (glassdoor.com)
- InfoJobs (infojobs.net)
- Google (google.com)

#### 4. .NET SDK

```bash
# Check .NET version
dotnet --version

# Should be .NET 10.0 or later
```

## Configuration

### Integration Tests Configuration

Integration tests use `appsettings.json` for configuration:

```json
{
  "Ghost": {
    "Extensions": {
      "LinkedIn": {
        "Enabled": true
      },
      "Indeed": {
        "Enabled": true
      },
      "Google": {
        "Enabled": true
      },
      "Glassdoor": {
        "Enabled": false
      },
      "InfoJobs": {
        "Enabled": false
      }
    }
  }
}
```

### Smoke Tests Configuration

Smoke tests use environment variables for configuration. All variables are optional with sensible defaults.

| Variable | Default | Description |
|----------|---------|-------------|
| `GHOST_SMOKE_BASE_URL` | `http://localhost:8080` | Ghost API base URL for smoke tests |
| `GHOST_ADMIN_API_KEY` | (none) | Admin API key for authentication |

### Setting Environment Variables

#### Linux/macOS (bash/zsh)

```bash
# Temporary (current session)
export GHOST_SMOKE_BASE_URL="http://localhost:8080"
export GHOST_ADMIN_API_KEY="your-admin-api-key"

# Permanent (add to ~/.bashrc or ~/.zshrc)
echo 'export GHOST_SMOKE_BASE_URL="http://localhost:8080"' >> ~/.bashrc
echo 'export GHOST_ADMIN_API_KEY="your-admin-api-key"' >> ~/.bashrc
source ~/.bashrc
```

#### Windows (PowerShell)

```powershell
# Temporary (current session)
$env:GHOST_SMOKE_BASE_URL="http://localhost:8080"
$env:GHOST_ADMIN_API_KEY="your-admin-api-key"

# Permanent (add to system environment variables)
[System.Environment]::SetEnvironmentVariable('GHOST_SMOKE_BASE_URL', 'http://localhost:8080', 'User')
[System.Environment]::SetEnvironmentVariable('GHOST_ADMIN_API_KEY', 'your-admin-api-key', 'User')
```

#### Using .env File (Not Recommended for Production)

Create a `.env` file in the smoke test directory:

```env
GHOST_SMOKE_BASE_URL=http://localhost:8080
GHOST_ADMIN_API_KEY=your-admin-api-key
```

Then load it:

```bash
# Linux/macOS
export $(cat .env | xargs)

# Windows PowerShell
Get-Content .env | ForEach-Object { $var = $_.Split('='); [System.Environment]::SetEnvironmentVariable($var[0], $var[1], 'Process') }
```

## Execution

### Run Integration Tests

```bash
# Run all integration tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration"

# Run specific platform integration tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration&Platform=LinkedIn"

# Run flow integration tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Integration&Flow=EndToEnd"
```

### Run Smoke Tests

```bash
# Run all smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke"

# Run specific platform smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke&Platform=LinkedIn"

# Run flow smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Category=Smoke&Flow=MultiPlatform"
```

### Run All Tests

```bash
# Basic execution
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj

# With detailed output
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed"

# With build
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --build
```

### Run Single Platform Tests

Filter by platform trait:

```bash
# LinkedIn only (both integration and smoke)
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Platform=LinkedIn"

# Indeed only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Platform=Indeed"

# Glassdoor only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Platform=Glassdoor"

# InfoJobs only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Platform=InfoJobs"

# Google only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Platform=Google"
```

### Run Specific Flow Tests

Filter by flow trait:

```bash
# End-to-end flow tests only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Flow=EndToEnd"

# Multi-platform aggregation tests only
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "Flow=MultiPlatform"
```

### Run Specific Test Method

```bash
# Run a specific test
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "FullyQualifiedName~Search_RealJobs_Returns_Populated_Fresh_Data"

# Run tests matching a pattern
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "FullyQualifiedName~Search"
```

### Run with Output

Show test results with ITestOutputHelper content:

```bash
# Detailed console output
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed"

# Save results to file
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "trx;LogFileName=smoke-test-results.trx"

# Multiple loggers
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed" --logger "trx;LogFileName=smoke-test-results.trx"
```

### Run with Parallel Execution

By default, tests run in parallel. To run sequentially:

```bash
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --parallel none
```

### Run with Timeout

Set a timeout for all tests:

```bash
# 5 minute timeout
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --timeout 300
```

## Interpreting Results

### Success Criteria

Smoke tests pass when:

1. **Non-empty results**: Each platform returns at least one job
2. **Required fields present**: Id, Title, Company, Url, Source are not null/empty
3. **Fresh data**: Jobs posted within 90 days (configurable)
4. **No duplicates**: All job IDs are unique
5. **Reachable URLs**: Job URLs return HTTP 200-299
6. **Valid platform IDs**: Platform-specific ID format is correct

### Sample Successful Output

```
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 2m 34s

=== Sample Job Data ===
ID: linkedin-1234567890
Title: Senior Software Engineer
Company: Tech Corp
Location: Remote
URL: https://linkedin.com/jobs/view/1234567890
Posted: 2025-12-15
Source: LinkedIn

=== Platform Distribution ===
Platforms contributing data: 4
  - LinkedIn: 5 jobs
  - Indeed: 3 jobs
  - Glassdoor: 2 jobs
  - InfoJobs: 1 jobs

=== Freshness Validation ===
Fresh jobs (within 90 days): 15/15
```

### Common Failures

#### 1. Connection Refused

**Symptom**:
```
System.Net.Http.HttpRequestException: Connection refused
```

**Cause**: Ghost instance is not running or wrong URL

**Solution**:
```bash
# Check if Ghost is running
curl https://localhost:5001/health

# If not running, start Ghost
cd src/Hosting/Ghost.Hosting
dotnet run

# Or check the URL
export GHOST_BASE_URL="https://correct-url.com"
```

#### 2. Empty Results

**Symptom**:
```
Expected results not to be empty because search should return at least one job
```

**Cause**: Scrapers broken, site changed, or rate limiting

**Solution**:
```bash
# Check scraper logs
# Look for errors in Ghost logs

# Try a different search query
# The test uses "software engineer" - try "developer" or "engineer"

# Check if the site is accessible
curl -I https://www.linkedin.com/jobs/
```

#### 3. Missing Fields

**Symptom**:
```
Expected job.Title not to be empty
Expected job.Company not to be empty
```

**Cause**: Parser needs updating or site structure changed

**Solution**:
```bash
# Check the actual HTML structure
# Update the parser in the relevant plugin

# Example: Update LinkedIn parser
# src/Plugins/Ghost.Plugin.LinkedIn/Parsers/LinkedInJobParser.cs
```

#### 4. Stale Data

**Symptom**:
```
Expected job.PostedAt to be within 90.00d of the current date
```

**Cause**: Scraping not running or frequency issues

**Solution**:
```bash
# Check scraping schedule
# Ensure background jobs are running

# Manually trigger a scrape
# POST /api/admin/scrape/trigger

# Check last scrape time
# GET /api/health/platforms
```

#### 5. Duplicates

**Symptom**:
```
Expected results to contain only unique job IDs
```

**Cause**: Deduplication logic issue

**Solution**:
```bash
# Check deduplication implementation
# src/Core/Ghost/Services/JobDeduplicationService.cs

# Verify job ID generation
# Ensure platform-specific IDs are unique
```

#### 6. Rate Limiting

**Symptom**:
```
System.Net.Http.HttpRequestException: Too Many Requests (429)
```

**Cause**: Too many requests to the platform

**Solution**:
```bash
# Add delays between requests
# Update rate limiting configuration

# Use different test criteria
# Reduce MaxResults in test

# Wait and retry
# Most rate limits reset after 15-60 minutes
```

### Reading Test Output

Test output includes:

1. **Test name**: What is being tested
2. **Assertions**: What was validated
3. **Sample data**: First few jobs for human verification
4. **Statistics**: Job counts, platform distribution, freshness

Example:
```
=== Step 1: Searching for jobs ===
Query: software engineer
Max Results: 10

Found 10 jobs

=== Sample Job Data ===
ID: linkedin-1234567890
Title: Senior Software Engineer
Company: Tech Corp
Location: Remote
URL: https://linkedin.com/jobs/view/1234567890
Posted: 2025-12-15
Source: LinkedIn

=== Platform Distribution ===
Platforms contributing data: 4
  - LinkedIn: 5 jobs
  - Indeed: 3 jobs
  - Glassdoor: 2 jobs
  - InfoJobs: 1 jobs

=== Freshness Validation ===
Fresh jobs (within 90 days): 10/10
```

## Troubleshooting

### General Troubleshooting Steps

1. **Check Ghost is running**:
   ```bash
   curl https://localhost:5001/health
   ```

2. **Check environment variables**:
   ```bash
   echo $GHOST_BASE_URL
   echo $GHOST_API_KEY
   ```

3. **Check network connectivity**:
   ```bash
   ping linkedin.com
   ping indeed.com
   ```

4. **Check Ghost logs**:
   ```bash
   # Look for errors in Ghost output
   # Check for scraper failures
   ```

5. **Run with verbose output**:
   ```bash
   dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --logger "console;verbosity=detailed"
   ```

### Platform-Specific Issues

#### LinkedIn

**Common Issues**:
- Requires authentication for some searches
- Rate limiting after ~100 requests
- Job IDs change over time

**Solutions**:
- Use public job searches
- Add delays between requests
- Handle expired job IDs gracefully

#### Indeed

**Common Issues**:
- Anti-bot detection
- Geographic restrictions
- Job posting expiration

**Solutions**:
- Use realistic user agent headers
- Test with different locations
- Handle expired postings

#### Glassdoor

**Common Issues**:
- Requires login for some features
- Limited job data without authentication
- Company reviews mixed with jobs

**Solutions**:
- Focus on public job listings
- Validate available fields only
- Filter out non-job content

#### InfoJobs

**Common Issues**:
- Spanish language results
- Limited geographic coverage
- Different job ID format

**Solutions**:
- Use Spanish queries for testing
- Test with Spanish locations
- Update ID validation logic

#### Google

**Common Issues**:
- Aggregates from multiple sources
- Duplicate jobs from same source
- Limited job details

**Solutions**:
- Expect duplicates, validate deduplication
- Focus on available fields
- Use as aggregation test, not source test

### Debugging Tips

1. **Run single test**:
   ```bash
   dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj --filter "FullyQualifiedName~Search_RealJobs_Returns_Populated_Fresh_Data"
   ```

2. **Add breakpoints**:
   - Open test file in IDE
   - Set breakpoint on assertion
   - Run test in debug mode

3. **Inspect variables**:
   - Check `results` variable
   - Examine individual `job` objects
   - Validate field values

4. **Check HTTP requests**:
   - Use Fiddler or Charles Proxy
   - Inspect request/response headers
   - Verify authentication

5. **Test API directly**:
   ```bash
   # Test Ghost API directly
   curl -X POST https://localhost:5001/api/jobs/search \
     -H "Content-Type: application/json" \
     -d '{"query":"software engineer","maxResults":10}'
   ```

## CI/CD Considerations

### Why Smoke Tests Are Manual-Only

Smoke tests are **not** suitable for automatic CI/CD pipelines because:

1. **External Dependencies**: Depend on production job platforms
2. **Rate Limiting**: Can trigger platform rate limits
3. **Flakiness**: External sites can change without notice
4. **Slow Execution**: Real network requests take time
5. **False Positives**: Failures may not indicate code issues

### When to Run Smoke Tests

Run smoke tests:

1. **Before Release**: Validate all scrapers working
2. **After Major Changes**: Verify scraper updates
3. **When Site Changes**: After platform UI updates
4. **Periodically**: Weekly or bi-weekly health check
5. **On Demand**: When investigating issues

### Integrating into Release Checklist

Add to release checklist:

```markdown
## Pre-Release Checklist

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] **Smoke tests pass (manual)**
  - [ ] LinkedIn smoke tests
  - [ ] Indeed smoke tests
  - [ ] Glassdoor smoke tests
  - [ ] InfoJobs smoke tests
  - [ ] Google smoke tests
  - [ ] End-to-end flow tests
- [ ] Documentation updated
- [ ] Release notes prepared
```

### Running Smoke Tests in CI (Not Recommended)

If you must run smoke tests in CI:

1. **Use separate pipeline**: Don't block main CI
2. **Add delays**: Between test runs
3. **Use staging environment**: Not production
4. **Monitor rate limits**: Track platform usage
5. **Allow failures**: Don't block deployment

Example GitHub Actions workflow:

```yaml
name: Smoke Tests (Manual)

on:
  workflow_dispatch:
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday

jobs:
  smoke-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Run smoke tests
        run: dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj
        continue-on-error: true  # Don't fail the workflow
      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: smoke-test-results
          path: '**/*.trx'
```

## Best Practices

### 1. Run Regularly

- Weekly or bi-weekly
- Before releases
- After major changes

### 2. Document Failures

- Create issues for failures
- Include test output
- Note platform changes

### 3. Update Tests

- When platforms change
- When scrapers updated
- When new fields added

### 4. Use Sample Data

- Verify data quality manually
- Check for unexpected patterns
- Validate field formats

### 5. Monitor Trends

- Track job counts over time
- Note freshness trends
- Identify degrading platforms

## Additional Resources

- [Project README](../../../README.md)
- [Testing Reference](../testing-reference.md)
- [Agent Playbook](../agent-playbook.md)
- [.NET 10 Operations](../dotnet10-ops.md)

## Support

For issues or questions:

1. Check this guide first
2. Review test output
3. Check Ghost logs
4. Create issue with details
