---
name: dotnet-harness-manifest
description:
  'Skill manifest management for dotnet-harness. Tracks skill dependencies, conflicts, version compatibility, and
  provides validation and resolution tools. Triggers on: skill manifest, dependency resolution, skill compatibility,
  version conflicts, build manifest, validate dependencies.'
targets: ['*']
tags: ['dotnet', 'skill', 'dotnet-harness', 'manifest', 'dependencies']
version: '0.0.1'
author: 'dotnet-harness'
claudecode:
  model: inherit
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Edit', 'Write']
opencode:
  mode: subagent
  tools:
    bash: true
    edit: true
    write: true
copilot:
  tools: ['read', 'search', 'execute', 'edit']
---

# dotnet-harness-manifest

Skill manifest management for dotnet-harness toolkit. Tracks dependencies between skills, version compatibility, and
conflicts.

## When to Use

Load this skill when:

- Resolving skill dependencies before invocation
- Validating skill compatibility
- Building the skill manifest
- Detecting version conflicts
- Analyzing skill dependency graphs

## Key Concepts

### Manifest Structure

Each skill declares dependencies and conflicts in frontmatter:

```yaml
depends_on:
  - dotnet-version-detection
  - dotnet-project-analysis
conflicts_with:
  - legacy-ef-core-patterns
version: '2.1.0'
```

### Dependency Types

1. **Required** (`depends_on`): Must be loaded before this skill
2. **Optional** (`optional`): Loaded if available, skipped if missing
3. **Conflicts** (`conflicts_with`): Cannot be used with these skills

### Version Semantics

- Follows semantic versioning (MAJOR.MINOR.PATCH)
- Breaking changes bump MAJOR
- New features bump MINOR
- Bug fixes bump PATCH
- Version ranges supported: `^1.0.0`, `~1.2.0`, `>=1.0.0 <2.0.0`

## Workflow

1. **Build manifest** -- Run [dotnet-harness:build-manifest] to generate `.rulesync/manifest/skill-manifest.json`
2. **Validate dependencies** -- Check all skills have valid dependency declarations
3. **Resolve conflicts** -- Identify and report skill conflicts
4. **Generate graph** -- Create dependency visualization for complex scenarios

## Commands

### Build Manifest

```bash
/dotnet-harness:build-manifest
```

Generates `skill-manifest.json` from all skill frontmatter.

### Validate Dependencies

```bash
/dotnet-harness:validate-dependencies <skill-name>
```

Checks if a skill's dependencies are satisfiable.

### Check Conflicts

```bash
/dotnet-harness:check-conflicts
```

Identifies skill conflicts across the toolkit.

### Dependency Graph

```bash
/dotnet-harness:graph --format mermaid
```

Generates dependency visualization.

## Manifest Schema

Location: `.rulesync/manifest/schema.json`

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "version": { "type": "string" },
    "generated_at": { "type": "string", "format": "date-time" },
    "skills": {
      "type": "object",
      "patternProperties": {
        "^[a-z-]+$": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "version": { "type": "string" },
            "description": { "type": "string" },
            "tags": { "type": "array", "items": { "type": "string" } },
            "depends_on": { "type": "array", "items": { "type": "string" } },
            "optional": { "type": "array", "items": { "type": "string" } },
            "conflicts_with": { "type": "array", "items": { "type": "string" } },
            "file_path": { "type": "string" },
            "line_count": { "type": "integer" }
          }
        }
      }
    }
  }
}
```

## Dependency Resolution Algorithm

1. **Load target skill**
2. **Collect dependencies** (recursive, breadth-first)
3. **Detect cycles** (fail fast on circular deps)
4. **Check conflicts** (fail if conflict found)
5. **Topological sort** (dependency order)
6. **Validate versions** (ensure compatibility)

## Example Usage

### Basic Dependency Check

```bash
# Check if dotnet-efcore-patterns can be loaded
dotnet-harness:validate-dependencies dotnet-efcore-patterns

# Output:
# ✓ dotnet-version-detection (found)
# ✓ dotnet-project-analysis (found)
# ✓ All dependencies satisfied
```

### Conflict Detection

```bash
# Check for conflicts
dotnet-harness:check-conflicts

# Output:
# ⚠ Conflict detected:
#   dotnet-efcore-patterns conflicts with: legacy-ef-core-patterns
#   Both cannot be loaded simultaneously
```

### Build Full Manifest

```bash
# Generate complete manifest
dotnet-harness:build-manifest

# Creates: .rulesync/manifest/skill-manifest.json
# Size: ~50KB for 131 skills
```

## Best Practices

- **Declare minimal dependencies**: Only require what's essential
- **Use optional for enhancements**: Features that improve but aren't required
- **Document conflicts**: Explain why skills conflict in description
- **Version skills**: Always include version for tracking
- **Test manifest builds**: Run validation before committing

## Integration with Advisor

The [skill:dotnet-advisor] uses the manifest to:

- Route to appropriate skills based on dependencies
- Warn about version mismatches
- Suggest compatible alternatives
- Build dependency chains for complex scenarios

## References

- [skill:dotnet-advisor] -- skill routing and delegation
- [skill:dotnet-project-analysis] -- project context detection
- [skill:dotnet-version-detection] -- TFM and SDK detection
- `.rulesync/manifest/schema.json` -- manifest JSON schema

## Code Navigation (Serena MCP)

**Primary approach:** Use Serena symbol operations for efficient code navigation:

1. **Find definitions**: `serena_find_symbol` instead of text search
2. **Understand structure**: `serena_get_symbols_overview` for file organization
3. **Track references**: `serena_find_referencing_symbols` for impact analysis
4. **Precise edits**: `serena_replace_symbol_body` for clean modifications

**When to use Serena vs traditional tools:**

- **Use Serena**: Navigation, refactoring, dependency analysis, precise edits
- **Use Read/Grep**: Reading full files, pattern matching, simple text operations
- **Fallback**: If Serena unavailable, traditional tools work fine

**Example workflow:**

```text
# Instead of:
Read: src/Services/OrderService.cs
Grep: "public void ProcessOrder"

# Use:
serena_find_symbol: "OrderService/ProcessOrder"
serena_get_symbols_overview: "src/Services/OrderService.cs"
```
