# Platform Tests to Plugins Migration

## Overview

This migration moves test projects from `tests/Platforms` to `tests/Plugins` to align with the plugin-based architecture. The migration is non-destructive - legacy files remain in place for easy rollback.

## Migration Date

2026-02-13

## Project Mapping

| Old Project Path | New Project Path | Notes |
|-----------------|------------------|-------|
| `tests/Platforms/Ghost.Platform.LinkedIn.Tests/Ghost.Platform.LinkedIn.Tests.csproj` | `tests/Plugins/Ghost.Plugin.LinkedIn.Tests/Ghost.Plugin.LinkedIn.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.LinkedIn.Integration/Ghost.Platform.LinkedIn.Integration.csproj` | `tests/Plugins/Ghost.Plugin.LinkedIn.Integration/Ghost.Plugin.LinkedIn.Integration.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Indeed.Tests/Ghost.Platform.Indeed.Tests.csproj` | `tests/Plugins/Ghost.Plugin.Indeed.Tests/Ghost.Plugin.Indeed.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Indeed.Integration/Ghost.Platform.Indeed.Integration.csproj` | `tests/Plugins/Ghost.Plugin.Indeed.Integration/Ghost.Plugin.Indeed.Integration.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Google.Tests/Ghost.Platform.Google.Tests.csproj` | `tests/Plugins/Ghost.Plugin.Google.Tests/Ghost.Plugin.Google.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Google.Integration/Ghost.Platform.Google.Integration.csproj` | `tests/Plugins/Ghost.Plugin.Google.Integration/Ghost.Plugin.Google.Integration.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Glassdoor.Tests/Ghost.Platform.Glassdoor.Tests.csproj` | `tests/Plugins/Ghost.Plugin.Glassdoor.Tests/Ghost.Plugin.Glassdoor.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Glassdoor.Integration/Ghost.Platform.Glassdoor.Integration.csproj` | `tests/Plugins/Ghost.Plugin.Glassdoor.Integration/Ghost.Plugin.Glassdoor.Integration.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.InfoJobs.Tests/Ghost.Platform.InfoJobs.Tests.csproj` | `tests/Plugins/Ghost.Plugin.InfoJobs.Tests/Ghost.Plugin.InfoJobs.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.InfoJobs.Integration/Ghost.Platform.InfoJobs.Integration.csproj` | `tests/Plugins/Ghost.Plugin.InfoJobs.Integration/Ghost.Plugin.InfoJobs.Integration.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.OpenAI.Tests/Ghost.Platform.OpenAI.Tests.csproj` | `tests/Plugins/Ghost.Plugin.OpenAI.Tests/Ghost.Plugin.OpenAI.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Anthropic.Tests/Ghost.Platform.Anthropic.Tests.csproj` | `tests/Plugins/Ghost.Plugin.Anthropic.Tests/Ghost.Plugin.Anthropic.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.X.E2E/Ghost.Platform.X.E2E.csproj` | `tests/Plugins/Ghost.Plugin.X.E2E/Ghost.Plugin.X.E2E.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.X.Tests/Ghost.Platform.X.Tests.csproj` | `tests/Plugins/Ghost.Plugin.X.Tests/Ghost.Plugin.X.Tests.csproj` | Reuses existing test files |
| `tests/Platforms/Ghost.Platform.Common.Tests/Ghost.Platform.Common.Tests.csproj` | `tests/Plugins/Ghost.Plugin.Common.Tests/Ghost.Plugin.Common.Tests.csproj` | Reuses existing test files |

## Implementation Details

### Non-Destructive Approach

- Legacy test source files remain in `tests/Platforms/` directories
- New plugin test projects reference legacy files via `<Compile Include="..\..\Platforms\...\**\*.cs" />`
- No test files were moved or deleted
- Legacy csproj files remain in place (not deleted)

### Project Reference Changes

New plugin test projects reference plugin production projects instead of platform projects:

| Test Project | Old Production Reference | New Production Reference |
|--------------|-------------------------|-------------------------|
| Ghost.Plugin.LinkedIn.Tests | Ghost.Platform.LinkedIn | Ghost.Plugin.LinkedIn |
| Ghost.Plugin.Indeed.Tests | Ghost.Platform.Indeed | Ghost.Plugin.Indeed |
| Ghost.Plugin.Google.Tests | Ghost.Platform.Google | Ghost.Plugin.Google |
| Ghost.Plugin.Glassdoor.Tests | Ghost.Platform.Glassdoor | Ghost.Plugin.Glassdoor |
| Ghost.Plugin.InfoJobs.Tests | Ghost.Platform.InfoJobs | Ghost.Plugin.InfoJobs |
| Ghost.Plugin.OpenAI.Tests | Ghost.Platform.OpenAI | Ghost.Plugin.OpenAI |
| Ghost.Plugin.Anthropic.Tests | Ghost.Platform.Anthropic | Ghost.Plugin.Anthropic |
| Ghost.Plugin.X.Tests | Ghost.Platform.X | Ghost.Plugin.X |
| Ghost.Plugin.X.E2E | Ghost.Platform.X | Ghost.Plugin.X |
| Ghost.Plugin.Common.Tests | Ghost.Platform.Common | Ghost.Platform.Common (unchanged) |

### Solution Changes

**Removed from Ghost.sln:**
- Ghost.Platform.OpenAI.Tests
- Ghost.Platform.LinkedIn.Tests
- Ghost.Platform.Indeed.Tests
- Ghost.Platform.Glassdoor.Tests
- Ghost.Platform.InfoJobs.Tests
- Ghost.Platform.X.E2E
- Ghost.Platform.LinkedIn.Integration
- Ghost.Platform.Indeed.Integration
- Ghost.Platform.Google.Integration
- Ghost.Platform.Glassdoor.Integration
- Ghost.Platform.InfoJobs.Integration

**Added to Ghost.sln:**
- Ghost.Plugin.LinkedIn.Tests (already existed)
- Ghost.Plugin.LinkedIn.Integration
- Ghost.Plugin.Indeed.Tests
- Ghost.Plugin.Indeed.Integration
- Ghost.Plugin.Google.Tests
- Ghost.Plugin.Google.Integration
- Ghost.Plugin.Glassdoor.Tests
- Ghost.Plugin.Glassdoor.Integration
- Ghost.Plugin.InfoJobs.Tests
- Ghost.Plugin.InfoJobs.Integration
- Ghost.Plugin.OpenAI.Tests
- Ghost.Plugin.Anthropic.Tests
- Ghost.Plugin.X.Tests
- Ghost.Plugin.X.E2E
- Ghost.Plugin.Common.Tests

## CI Impact

### Test Categories and Traits

**No changes required.** Test categories and traits are preserved because:
- Test source files are unchanged (referenced via `<Compile Include>` )
- Test attributes (e.g., `[Trait("Category", "Integration")]`) remain in the original test files
- CI filters based on traits will continue to work without modification

### Path Filters

**No changes required.** CI path filters should continue to work because:
- Test files remain in `tests/Platforms/` directories
- New csproj files are in `tests/Plugins/` but reference the same test files
- If CI filters on csproj paths, update filters to include `tests/Plugins/`

### Build Configuration

**No changes required.** The solution file has been updated to reference the new plugin test projects, so standard build commands (`dotnet build Ghost.sln`, `dotnet test Ghost.sln`) will work without modification.

## Rollback Plan

If issues arise, rollback can be performed in minutes:

### Step 1: Restore Solution File
```bash
cp Ghost.sln.backup Ghost.sln
```

### Step 2: Remove New Plugin Test Projects
```bash
rm -rf tests/Plugins/Ghost.Plugin.LinkedIn.Integration
rm -rf tests/Plugins/Ghost.Plugin.Indeed.Tests
rm -rf tests/Plugins/Ghost.Plugin.Indeed.Integration
rm -rf tests/Plugins/Ghost.Plugin.Google.Tests
rm -rf tests/Plugins/Ghost.Plugin.Google.Integration
rm -rf tests/Plugins/Ghost.Plugin.Glassdoor.Tests
rm -rf tests/Plugins/Ghost.Plugin.Glassdoor.Integration
rm -rf tests/Plugins/Ghost.Plugin.InfoJobs.Tests
rm -rf tests/Plugins/Ghost.Plugin.InfoJobs.Integration
rm -rf tests/Plugins/Ghost.Plugin.OpenAI.Tests
rm -rf tests/Plugins/Ghost.Plugin.Anthropic.Tests
rm -rf tests/Plugins/Ghost.Plugin.X.Tests
rm -rf tests/Plugins/Ghost.Plugin.X.E2E
rm -rf tests/Plugins/Ghost.Plugin.Common.Tests
```

### Step 3: Restore Ghost.Plugin.LinkedIn.Tests (if needed)
```bash
# If the existing Ghost.Plugin.LinkedIn.Tests was modified, restore from git
git checkout HEAD -- tests/Plugins/Ghost.Plugin.LinkedIn.Tests/Ghost.Plugin.LinkedIn.Tests.csproj
```

### Step 4: Verify Build
```bash
dotnet restore Ghost.sln
dotnet build Ghost.sln --no-restore --warnaserror
dotnet test Ghost.sln --no-build
```

## Verification

### Build Verification
```bash
dotnet format Ghost.sln --verify-no-changes
dotnet restore Ghost.sln
dotnet build Ghost.sln --no-restore --warnaserror
```

### Test Verification
```bash
dotnet test Ghost.sln --no-build
```

### Project Structure Verification
```bash
# Verify all new plugin test projects exist
ls tests/Plugins/Ghost.Plugin.*.Tests/
ls tests/Plugins/Ghost.Plugin.*.Integration/
ls tests/Plugins/Ghost.Plugin.*.E2E/

# Verify legacy test files still exist
ls tests/Platforms/Ghost.Platform.*.Tests/
ls tests/Platforms/Ghost.Platform.*.Integration/
ls tests/Platforms/Ghost.Platform.*.E2E/
```

## Known Issues and Caveats

### Google and Anthropic Tests
- Google and Anthropic test projects were previously quarantined (not in solution)
- Both are now included in the solution as part of this migration
- These tests may have reliability issues that need to be addressed separately
- See issue `Ghost-iapl` for re-enabling these test suites

### OpenAI Tests
- OpenAI tests have a special configuration to exclude `OpenAIClientTests.cs` from default compilation
- This configuration is preserved in the new plugin test project

### X Platform Tests
- X platform has both unit tests (`Ghost.Plugin.X.Tests`) and E2E tests (`Ghost.Plugin.X.E2E`)
- Both are migrated to plugin-aligned projects

### Common Tests
- Ghost.Plugin.Common.Tests continues to reference Ghost.Platform.Common (no plugin equivalent exists)
- This is intentional as Ghost.Platform.Common is shared infrastructure used by multiple platforms

## Follow-up Work

1. **Issue**: `Ghost-iapl` - Re-enable Google and Anthropic test suites
   - These tests are now in the solution but may have reliability issues
   - Root-cause analysis and deterministic stabilization evidence needed before full re-enablement

2. **Cleanup** (optional, after verification period):
   - Remove legacy `tests/Platforms/` test csproj files
   - Remove legacy `tests/Platforms/` test source files
   - Update CI path filters if they reference old paths

## References

- Original task: Ghost-7boy.2 and Ghost-7boy.3
- Solution file backup: `Ghost.sln.backup`
- Migration date: 2026-02-13
