# Plugin End-to-End Tests

## Overview

End-to-end tests for plugins are intentionally skipped by default in CI/CD.

## Why Tests Are Skipped

These tests require:
- External API access (Indeed, LinkedIn, Google, Glassdoor, InfoJobs)
- Browser automation (Playwright)
- Valid API keys and authentication
- External infrastructure that may not be available in CI

## Test Categories

| Plugin | Tests | Skip Reason |
|--------|-------|-------------|
| Google | 17 | Requires GHOST_E2E=1 |
| Glassdoor | 17 | Requires GHOST_E2E=1 |
| LinkedIn | 26 | Requires GHOST_E2E=1 |
| Indeed | 16 | Requires GHOST_E2E=1 |
| InfoJobs | 16 | Requires GHOST_E2E=1 |

## Running End-to-End Tests

To enable end-to-end tests, set the environment variable:

```bash
export GHOST_E2E=1
dotnet test tests/Plugins/Ghost.Plugin.Google.End2EndTests/
```

Or run all end-to-end tests:

```bash
export GHOST_E2E=1
dotnet test tests/Plugins/ --filter "End2End"
```

## Explicitly Skipped Tests

The following tests are explicitly skipped due to DI configuration requirements:

- `GooglePluginE2ETests.ConfigureServices_RegistersGoogleJobClient`
- `GlassdoorPluginE2ETests.ConfigureServices_RegistersGlassdoorJobClient`
- `IndeedPluginE2ETests.ConfigureServices_RegistersIndeedJobClient`
- `LinkedInJobClientE2ETests.ApplyForJobAsync_ThrowsNotImplementedAsync`
