# Ghost Smoke Tests

Real data smoke tests for Ghost job platform scrapers.

## Purpose

These tests validate that scrapers return real, fresh job data from production endpoints. They run manually against a live Ghost instance with zero mocking.

## Structure

- **Assertions/** - Data quality assertion library
- **Platforms/** - Platform-specific smoke tests
- **Flows/** - End-to-end workflow tests

## Execution

Smoke tests are manual-only and should not run in CI pipelines.

```bash
# Set environment variables (optional)
export GHOST_BASE_URL="https://your-ghost-instance.com"
export GHOST_API_KEY="your-api-key"

# Run smoke tests
dotnet test tests/Smoke/Ghost.Smoke.Tests/Ghost.Smoke.Tests.csproj
```

## Notes

- Tests connect to real production endpoints
- No mocking or test doubles
- Validates actual data quality and freshness
- Requires network connectivity to production
