# Setup Ghost Action

Reusable composite action for setting up the Ghost build environment in GitHub Actions workflows.

## Features

- ✅ Sets up .NET SDK with version from global.json
- ✅ Caches NuGet packages for faster builds
- ✅ Optionally installs Playwright browsers for E2E tests
- ✅ Displays environment information in workflow summary

## Usage

### Basic Setup

```yaml
- uses: ./.github/actions/setup-ghost
```

### With Custom .NET Version

```yaml
- uses: ./.github/actions/setup-ghost
  with:
    dotnet-version: '10.0.x'
```

### With Playwright

```yaml
- uses: ./.github/actions/setup-ghost
  with:
    install-playwright: 'true'
    playwright-browsers: 'chromium firefox webkit'
```

### Full Example

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Ghost Environment
        uses: ./.github/actions/setup-ghost
        with:
          dotnet-version: '10.0.x'
          install-playwright: 'true'
          playwright-browsers: 'chromium'
      
      - name: Restore Dependencies
        run: dotnet restore Ghost.sln
      
      - name: Build
        run: dotnet build Ghost.sln --no-restore
      
      - name: Test
        run: dotnet test Ghost.sln --no-build
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `dotnet-version` | .NET SDK version to install | No | `10.0.x` |
| `install-playwright` | Install Playwright browsers | No | `false` |
| `playwright-browsers` | Browsers to install (space-separated) | No | `chromium` |

## Outputs

| Output | Description |
|--------|-------------|
| `cache-hit` | Whether NuGet cache was successfully restored |

## Cache Strategy

This action caches NuGet packages based on the hash of all `.csproj` files in the repository. This significantly speeds up subsequent workflow runs.

Cache key format: `{os}-nuget-{hash-of-csproj-files}`

## Environment Info

The action automatically adds build environment information to the workflow summary:
- Operating System
- .NET SDK Version
- NuGet Cache Status
- Playwright Installation Status (if enabled)
