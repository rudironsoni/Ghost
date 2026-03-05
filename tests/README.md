# Ghost Test Suite Documentation

## Overview

Ghost uses a comprehensive test suite with multiple categories of tests. Most tests **run by default**, but you can disable specific categories using environment variables. This document explains how to configure and run all test categories.

## Test Categories

| Category | Location | Execution | Default Status |
|----------|----------|-----------|----------------|
| **Unit Tests** | `tests/Unit/**/*.Tests` | Fast, in-memory | Enabled |
| **Integration Tests** | `tests/Integration/**/*.Tests` | In-process with mocks | Enabled |
| **System Tests** | `tests/System/**/*.Tests` | Browser automation | Enabled |
| **End-to-End (E2E) Tests** | `tests/E2E/**/*.Tests` | Real external APIs | **Enabled** |
| **Smoke Tests** | `tests/Smoke/**/*.Tests` | HTTP against running instance | Enabled |
| **Platform-Specific Tests** | `tests/Plugins/**/End2EndTests` | Real browser sessions | **Enabled** |

## Environment Variables

### Core Test Control Variables

#### `GHOST_DISABLE_E2E`
Disables all End-to-End tests that use `[End2EndFact]` or `[End2EndTheory]` attributes.

- **Value**: `true` or `1`
- **Default**: Enabled (tests run)
- **Purpose**: Controls execution of tests that interact with real external services
- **Example**:
  ```bash
  export GHOST_DISABLE_E2E=true
  dotnet test tests/Plugins/LinkedIn/Ghost.Plugin.LinkedIn.End2EndTests
  ```

### Platform-Specific Variables

These variables disable tests for specific job platforms. They work with the `[ConditionalFact("PlatformName")]` attribute.

#### `GHOST_DISABLE_LINKEDIN_TESTS`
Disables LinkedIn-specific integration and E2E tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests LinkedIn job scraping functionality
- **Example**:
  ```bash
  export GHOST_DISABLE_LINKEDIN_TESTS=true
  dotnet test tests/Plugins/LinkedIn --filter "Category=End2End"
  ```

#### `GHOST_DISABLE_GOOGLE_TESTS`
Disables Google-specific integration and E2E tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests Google job search functionality
- **Example**:
  ```bash
  export GHOST_DISABLE_GOOGLE_TESTS=true
  dotnet test tests/Plugins/Google --filter "Category=End2End"
  ```

#### `GHOST_DISABLE_INDEED_TESTS`
Disables Indeed-specific integration and E2E tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests Indeed job scraping functionality
- **Example**:
  ```bash
  export GHOST_DISABLE_INDEED_TESTS=true
  dotnet test tests/Plugins/Indeed --filter "Category=End2End"
  ```

#### `GHOST_DISABLE_GLASSDOOR_TESTS`
Disables Glassdoor-specific integration and E2E tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests Glassdoor job scraping functionality
- **Example**:
  ```bash
  export GHOST_DISABLE_GLASSDOOR_TESTS=true
  dotnet test tests/Plugins/Glassdoor --filter "Category=End2End"
  ```

#### `GHOST_DISABLE_INFOJOBS_TESTS`
Disables InfoJobs-specific integration and E2E tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests InfoJobs job scraping functionality
- **Example**:
  ```bash
  export GHOST_DISABLE_INFOJOBS_TESTS=true
  dotnet test tests/Plugins/InfoJobs --filter "Category=End2End"
  ```

#### `GHOST_DISABLE_MULTIPLATFORM_TESTS`
Disables multi-platform aggregation tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Tests that aggregate results from multiple job platforms
- **Example**:
  ```bash
  export GHOST_DISABLE_MULTIPLATFORM_TESTS=true
  dotnet test tests/Smoke --filter "Flow=MultiPlatform"
  ```

#### `GHOST_DISABLE_E2E_TESTS`
Disables end-to-end integration tests.

- **Value**: `true` or `false`
- **Default**: Enabled (tests run)
- **Purpose**: Broad control for E2E test scenarios
- **Example**:
  ```bash
  export GHOST_DISABLE_E2E_TESTS=true
  dotnet test tests/Smoke --filter "Category=End2End"
  ```

## Environment Variable Summary

| Variable | Values | Purpose |
|----------|--------|---------|
| `GHOST_DISABLE_E2E` | `true`, `1` | Master switch to disable End2End tests |
| `GHOST_DISABLE_LINKEDIN_TESTS` | `true`, `false` | LinkedIn platform tests |
| `GHOST_DISABLE_GOOGLE_TESTS` | `true`, `false` | Google platform tests |
| `GHOST_DISABLE_INDEED_TESTS` | `true`, `false` | Indeed platform tests |
| `GHOST_DISABLE_GLASSDOOR_TESTS` | `true`, `false` | Glassdoor platform tests |
| `GHOST_DISABLE_INFOJOBS_TESTS` | `true`, `false` | InfoJobs platform tests |
| `GHOST_DISABLE_MULTIPLATFORM_TESTS` | `true`, `false` | Multi-platform tests |
| `GHOST_DISABLE_E2E_TESTS` | `true`, `false` | E2E integration tests |

## Running Tests

### Quick Reference Commands

#### Run All Unit Tests (No Environment Variables Needed)
```bash
dotnet test tests/Unit
```

#### Run All Integration Tests (No Environment Variables Needed)
```bash
dotnet test tests/Integration
```

#### Run All Tests (E2E tests run by default)
```bash
dotnet test tests
```

#### Run All E2E Tests (Excluding LinkedIn)
```bash
export GHOST_DISABLE_LINKEDIN_TESTS=true
dotnet test tests --filter "Category=End2End"
```

#### Run LinkedIn E2E Tests Only (disable others)
```bash
export GHOST_DISABLE_GOOGLE_TESTS=true
export GHOST_DISABLE_INDEED_TESTS=true
export GHOST_DISABLE_GLASSDOOR_TESTS=true
export GHOST_DISABLE_INFOJOBS_TESTS=true
dotnet test tests/Plugins/LinkedIn --filter "Category=End2End"
```

#### Run Multiple Platform Tests (Disable specific platforms)
```bash
export GHOST_DISABLE_GLASSDOOR_TESTS=true
export GHOST_DISABLE_INFOJOBS_TESTS=true
dotnet test tests/Plugins --filter "Category=End2End"
```

#### Run Smoke Tests (Requires Ghost Instance Running)
```bash
export GHOST_SMOKE_BASE_URL="http://localhost:8080"
dotnet test tests/Smoke --filter "Category=Smoke"
```

### Linux/macOS (bash/zsh)

```bash
# Disable all E2E tests (skip them)
export GHOST_DISABLE_E2E=true

# Disable specific platforms
export GHOST_DISABLE_LINKEDIN_TESTS=true
export GHOST_DISABLE_GOOGLE_TESTS=true
export GHOST_DISABLE_INDEED_TESTS=true
export GHOST_DISABLE_GLASSDOOR_TESTS=true
export GHOST_DISABLE_INFOJOBS_TESTS=true
export GHOST_DISABLE_MULTIPLATFORM_TESTS=true
export GHOST_DISABLE_E2E_TESTS=true

# Run tests
# By default, all tests will run (unless disabled above)
dotnet test Ghost.sln --filter "Category=End2End"
```

### Windows (PowerShell)

```powershell
# Disable all E2E tests (skip them)
$env:GHOST_DISABLE_E2E="true"

# Disable specific platforms
$env:GHOST_DISABLE_LINKEDIN_TESTS="true"
$env:GHOST_DISABLE_GOOGLE_TESTS="true"
$env:GHOST_DISABLE_INDEED_TESTS="true"
$env:GHOST_DISABLE_GLASSDOOR_TESTS="true"
$env:GHOST_DISABLE_INFOJOBS_TESTS="true"
$env:GHOST_DISABLE_MULTIPLATFORM_TESTS="true"
$env:GHOST_DISABLE_E2E_TESTS="true"

# Run tests
# By default, all tests will run (unless disabled above)
dotnet test Ghost.sln --filter "Category=End2End"
```

### Windows (Command Prompt)

```cmd
REM Disable all E2E tests (skip them)
set GHOST_DISABLE_E2E=true

REM Disable specific platforms
set GHOST_DISABLE_LINKEDIN_TESTS=true
set GHOST_DISABLE_GOOGLE_TESTS=true
set GHOST_DISABLE_INDEED_TESTS=true
set GHOST_DISABLE_GLASSDOOR_TESTS=true
set GHOST_DISABLE_INFOJOBS_TESTS=true
set GHOST_DISABLE_MULTIPLATFORM_TESTS=true
set GHOST_DISABLE_E2E_TESTS=true

REM Run tests
REM By default, all tests will run (unless disabled above)
dotnet test Ghost.sln --filter "Category=End2End"
```

## Prerequisites for E2E Tests

### Browser Automation Setup

E2E tests use Playwright for browser automation. Install the required browsers:

```bash
# Install Playwright browsers
npx playwright install

# Or if using the .NET tool
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

Required browsers:
- Chromium (primary)
- Firefox (optional)
- WebKit (optional)

### External Service Requirements

E2E tests connect to real job platforms:

- **LinkedIn**: Requires valid session (cookies/credentials may be needed)
- **Indeed**: Public access, but rate-limited
- **Google**: Public access
- **Glassdoor**: Public access, some features require login
- **InfoJobs**: Public access

### Network Requirements

- Outbound HTTPS access to job platforms
- No proxy blocking (unless configured)
- Stable internet connection

## CI/CD Considerations

### E2E Tests Run by Default

E2E tests **now run by default** in local development. However, they may still be **disabled in CI/CD pipelines** because:

1. **External Dependencies**: Depend on production job platforms
2. **Rate Limiting**: Can trigger platform rate limits
3. **Flakiness**: External sites can change without notice
4. **Slow Execution**: Real browser automation takes time
5. **False Positives**: Failures may not indicate code issues

### Disabling E2E Tests in CI

To skip E2E tests in CI, set the disable variables:

```yaml
name: Unit Tests Only

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Run unit tests (skip E2E)
        env:
          GHOST_DISABLE_E2E: true
        run: dotnet test tests/Unit
```

### Running E2E Tests in CI (Optional)

If you want to run E2E tests in CI, use a separate workflow:

```yaml
name: E2E Tests (Manual)

on:
  workflow_dispatch:  # Manual trigger only
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install Playwright
        run: |
          npx playwright install chromium
      - name: Run E2E tests
        # No disable flags needed - tests run by default
        run: dotnet test tests/Plugins --filter "Category=End2End"
        continue-on-error: true  # Don't fail the workflow on external issues
```

### Recommended CI Strategy

| Test Type | CI Integration | Frequency |
|-----------|---------------|-----------|
| Unit Tests | Required gate | Every PR |
| Integration Tests | Required gate | Every PR |
| System Tests | Required gate | Every PR |
| E2E Tests | Optional/Manual | Weekly/Pre-release |
| Smoke Tests | Manual only | Pre-release |

## Test Attributes Reference

### `[End2EndFact]`
Marks a test as an End-to-End test. Runs unless `GHOST_DISABLE_E2E=true`.

```csharp
[End2EndFact]
[Trait("Category", "End2End")]
public async Task SearchJobsAsync_ReturnsResults()
{
    // Test code
}
```

### `[End2EndTheory]`
Marks a parameterized test as an End-to-End test. Runs unless `GHOST_DISABLE_E2E=true`.

```csharp
[End2EndTheory]
[InlineData("Software Engineer")]
[InlineData("Data Scientist")]
public async Task Search_WithQuery_ReturnsResults(string query)
{
    // Test code
}
```

### `[ConditionalFact("PlatformName")]`
Conditionally skips tests based on `GHOST_DISABLE_{PLATFORM}_TESTS` environment variable.

```csharp
[ConditionalFact("LinkedIn")]
[Trait("Category", "End2End")]
public async Task LinkedInSearch_ReturnsJobs()
{
    // Test code - runs unless GHOST_DISABLE_LINKEDIN_TESTS=true
}
```

## Troubleshooting

### Tests Are Running When They Should Be Skipped

If E2E tests are running when you want them skipped:

```bash
# Check environment variable is set correctly
echo $GHOST_DISABLE_E2E

# Should output: true

# Ensure the variable is exported
export GHOST_DISABLE_E2E=true

# Run with verbose output to see test execution details
dotnet test --logger "console;verbosity=detailed"
```

### Playwright Not Found

```bash
# Install Playwright browsers
npx playwright install

# Verify installation
npx playwright install --help
```

### Rate Limiting Errors

If you see `429 Too Many Requests`:

1. Reduce test frequency
2. Add delays between test runs
3. Use different test credentials
4. Wait 15-60 minutes before retrying

### Environment Variables for Local Development

```bash
# Create a local env file to disable specific platforms
cat > .env.local << 'EOF'
GHOST_DISABLE_LINKEDIN_TESTS=true
GHOST_DISABLE_INDEED_TESTS=true
GHOST_DISABLE_GLASSDOOR_TESTS=true
EOF

# Load environment variables
export $(cat .env.local | xargs)
```

## Related Documentation

- [Smoke Tests Guide](../docs/testing/smoke-tests.md) - Detailed smoke test documentation
- [Testing Lanes RFC](../docs/rfc/testing-lanes.md) - Parallelization strategy
- [Flaky Test Policy](../docs/flaky-test-policy.md) - Handling unreliable tests
- [Test Tier Audit](../docs/test-tier-audit.md) - Test categorization

## Additional Resources

### Test Scripts

The `tests/scripts/` directory contains helper scripts:

- `run-tests.sh` - Run test suites with proper configuration
- `pre-test.sh` - Pre-test setup (install browsers, etc.)
- `post-test.sh` - Post-test cleanup
- `validate-test-traits.sh` - Validate test trait configuration

### Run Settings

Test execution is configured in `Ghost.runsettings`:

```xml
<RunSettings>
  <RunConfiguration>
    <TestSessionTimeout>300000</TestSessionTimeout>
  </RunConfiguration>
</RunSettings>
```

### Filtering Tests

Use xUnit filters to run specific tests:

```bash
# By category
dotnet test --filter "Category=End2End"
dotnet test --filter "Category=Smoke"
dotnet test --filter "Category=Integration"

# By platform
dotnet test --filter "Platform=LinkedIn"
dotnet test --filter "Platform=Indeed"

# By trait
dotnet test --filter "TestType=End2End"
dotnet test --filter "Capability=RequiresProviderLive"

# Combined filters
dotnet test --filter "Category=End2End&Platform=LinkedIn"
```

## Support

For issues or questions:

1. Check this guide first
2. Review test output with `--logger "console;verbosity=detailed"`
3. Check the [Flaky Test Policy](../docs/flaky-test-policy.md)
4. Create an issue with test output and environment details
