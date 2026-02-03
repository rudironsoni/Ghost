# Configuration Structure Fix Plan

## Problem Statement

The Ghost configuration files have inconsistent structure across multiple files:

1. **Inconsistent Platform Placement**: Some platforms are configured at root level (`Indeed`, `Glassdoor`, `Google`) instead of under `Ghost:Extensions:`
2. **Missing Spanish Platforms**: InfoJobs and Tecnoempleo are missing from root configuration files
3. **Duplicate Settings**: Same settings appear in multiple places with different formats
4. **Inconsistent Environment Variable Names**: Mix of `GHOST__EXTENSIONS__*` and root-level variables

## Files Requiring Fixes

### High Priority (Core Configuration)
- `/.env.example` - Missing Spanish platforms under `Ghost:Extensions:`
- `/src/Ghost.WebApi/appsettings.json` - Platforms at root level instead of under `Ghost:Extensions:`
- `/src/Ghost.WebApi/appsettings.Development.json` - Platforms at root level instead of under `Ghost:Extensions:`

### Medium Priority (Examples)
- `/examples/config/appsettings.json` - Spanish platforms duplicated at root level
- `/examples/config/.env.example` - Needs alignment with corrected structure
- `/examples/README.md` - Update with corrected configuration patterns

## Root Cause Analysis

The inconsistency stems from:
- Some platforms using legacy configuration patterns
- Spanish platforms added later without updating root templates
- Examples created before standardization was complete

## Proposed Solution

### 1. Standardize Configuration Structure

**All platforms should be configured under `Ghost:Extensions:`**

```json
{
  "Ghost": {
    "Extensions": {
      "LinkedIn": { "Enabled": true },
      "Indeed": { "Enabled": true },
      "Glassdoor": { "Enabled": true },
      "Google": { "Enabled": true },
      "InfoJobs": { "Enabled": true },
      "Tecnoempleo": { "Enabled": true }
    }
  }
}
```

### 2. Remove Root-Level Platform Configuration

Eliminate duplicate platform configuration at root level:
- Remove `"Indeed": { ... }` from root
- Remove `"Glassdoor": { ... }` from root
- Remove `"Google": { ... }` from root
- Remove `"InfoJobs": { ... }` from root
- Remove `"Tecnoempleo": { ... }` from root

### 3. Standardize Environment Variables

Use consistent `GHOST__EXTENSIONS__{PLATFORM}__*` pattern:

```bash
# Correct pattern
GHOST__EXTENSIONS__INFOJOBS__ENABLED=true
GHOST__EXTENSIONS__INFOJOBS__CLIENTID=your_client_id
GHOST__EXTENSIONS__TECNOEMPLEO__ENABLED=true
GHOST__EXTENSIONS__TECNOEMPLEO__CLIENTID=your_client_id

# Remove inconsistent patterns
INFOJOBS_CLIENT_ID=...  # Remove
TECNOEMPLEO_CLIENT_ID=...  # Remove
```

## Implementation Plan

### Phase 1: Core Configuration Files

#### Task 1.1: Fix /.env.example
**Changes:**
- Add Spanish platform configuration under `Ghost:Extensions:`
- Remove any root-level platform variables

**Expected Result:**
```bash
# InfoJobs Configuration
GHOST__EXTENSIONS__INFOJOBS__ENABLED=true
GHOST__EXTENSIONS__INFOJOBS__COUNTRY=ES
GHOST__EXTENSIONS__INFOJOBS__CLIENTID=your_infojobs_client_id
GHOST__EXTENSIONS__INFOJOBS__CLIENTSECRET=your_infojobs_client_secret

# Tecnoempleo Configuration
GHOST__EXTENSIONS__TECNOEMPLEO__ENABLED=true
GHOST__EXTENSIONS__TECNOEMPLEO__CLIENTID=your_tecnoempleo_client_id
GHOST__EXTENSIONS__TECNOEMPLEO__CLIENTSECRET=your_tecnoempleo_client_secret
```

#### Task 1.2: Fix /src/Ghost.WebApi/appsettings.json
**Changes:**
- Move `Indeed`, `Glassdoor`, `Google` from root level to `Ghost:Extensions:`
- Add Spanish platforms under `Ghost:Extensions:`
- Remove root-level platform sections

**Expected Result:**
```json
{
  "Ghost": {
    "Extensions": {
      "LinkedIn": { "Enabled": true },
      "Indeed": { "Enabled": true, "Country": "ES" },
      "Glassdoor": { "Enabled": true },
      "Google": { "Enabled": true },
      "InfoJobs": { "Enabled": true },
      "Tecnoempleo": { "Enabled": true }
    }
  }
}
```

#### Task 1.3: Fix /src/Ghost.WebApi/appsettings.Development.json
**Changes:**
- Same as Task 1.2 but for development configuration

### Phase 2: Examples Configuration

#### Task 2.1: Fix /examples/config/appsettings.json
**Changes:**
- Remove root-level `InfoJobs` and `Tecnoempleo` sections
- Ensure all configuration is under `Ghost:Extensions:`

**Expected Result:**
```json
{
  "Ghost": {
    "Extensions": {
      "InfoJobs": {
        "Enabled": true,
        "Country": "ES",
        "Language": "es",
        "ClientId": "${INFOJOBS_CLIENT_ID}",
        "ClientSecret": "${INFOJOBS_CLIENT_SECRET}"
      },
      "Tecnoempleo": {
        "Enabled": true,
        "ClientId": "${TECNOEMPLEO_CLIENT_ID}",
        "ClientSecret": "${TECNOEMPLEO_CLIENT_SECRET}"
      }
    }
  }
}
```

#### Task 2.2: Fix /examples/config/.env.example
**Changes:**
- Use standardized environment variable names
- Remove inconsistent variable patterns

**Expected Result:**
```bash
# InfoJobs Configuration
GHOST__EXTENSIONS__INFOJOBS__CLIENTID=your_infojobs_client_id
GHOST__EXTENSIONS__INFOJOBS__CLIENTSECRET=your_infojobs_client_secret

# Tecnoempleo Configuration
GHOST__EXTENSIONS__TECNOEMPLEO__CLIENTID=your_tecnoempleo_client_id
GHOST__EXTENSIONS__TECNOEMPLEO__CLIENTSECRET=your_tecnoempleo_client_secret
```

#### Task 2.3: Update /examples/README.md
**Changes:**
- Update configuration examples to show corrected structure
- Add migration notes for users with existing configurations

### Phase 3: Validation

#### Task 3.1: Build Verification
- Verify solution builds successfully after changes
- Test configuration loading with Spanish platforms

#### Task 3.2: API Testing
- Test API startup with corrected configuration
- Verify Spanish platforms work correctly

## Technical Considerations

### Backward Compatibility
Some platforms may still read from root-level configuration. Need to:
1. Check if platform implementations use `IConfiguration` directly
2. Update platform configuration binding if necessary
3. Add migration notes for breaking changes

### Configuration Binding
Verify that platform extensions properly bind to `Ghost:Extensions:{Platform}` structure.

### Environment Variable Resolution
Ensure `DotNetEnv` properly resolves the new variable patterns.

## Risk Assessment

### High Risk
- Breaking existing configurations if users have root-level platform settings
- Platform extensions not reading from corrected configuration paths

### Mitigation Strategies
1. **Documentation**: Clear migration guide in README
2. **Validation**: Test configuration loading thoroughly
3. **Fallback**: Consider keeping legacy paths temporarily with deprecation warnings

## Success Criteria

- [ ] All configuration files use consistent `Ghost:Extensions:` structure
- [ ] No duplicate platform configuration at root level
- [ ] Spanish platforms properly configured in all files
- [ ] Environment variables use standardized naming
- [ ] Solution builds successfully
- [ ] API starts with Spanish platforms enabled
- [ ] Examples reflect corrected configuration patterns

## Testing Strategy

### Configuration Loading Test
```bash
# Test configuration parsing
dotnet run --project src/Ghost.WebApi --no-build --urls "http://localhost:5003"
```

### API Endpoint Test
```bash
# Test Spanish platform functionality
curl -X POST "http://localhost:5003/api/jobs/search" \
  -H "Content-Type: application/json" \
  -d '{"query":"desarrollador","platforms":["InfoJobs","Tecnoempleo"]}'
```

## Migration Notes for Users

Users with existing configurations should:
1. Move platform settings from root level to `Ghost:Extensions:`
2. Update environment variable names to use `GHOST__EXTENSIONS__*` pattern
3. Remove any duplicate platform configuration

## Next Steps

1. Execute `/start-work` to begin implementation
2. Follow the phased approach to minimize disruption
3. Validate each phase before proceeding to the next

---

**Plan Created**: Configuration structure standardization for consistent platform management across all Ghost configuration files.
