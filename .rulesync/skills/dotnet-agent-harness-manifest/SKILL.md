---
name: dotnet-agent-harness-manifest
category: developer-experience
subcategory: cli
description: 'Skill manifest management for dotnet-agent-harness. Tracks skill dependencies, conflicts, version compatibility, and provides validation and resolution tools. Triggers on: skill manifest, dependency resolution, skill compatibility, version conflicts, build manifest, validate dependencies.'
targets: ['*']
tags: [dotnet, skill, dotnet-agent-harness, manifest, dependencies]
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  model: inherit
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Edit', 'Write']
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
copilot:
  tools: ['read', 'search', 'execute', 'edit']
codexcli:
  short-description: '.NET skill guidance for dotnet-agent-harness-manifest'
geminicli: {}
antigravity: {}
---

# dotnet-agent-harness-manifest

Skill manifest management for dotnet-agent-harness toolkit. Tracks dependencies between skills, version compatibility,
and conflicts.

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

1. **Inspect catalog metadata** -- Run [dotnet-agent-harness:profile] for a catalog item or the whole catalog.
2. **Trace dependencies** -- Run [dotnet-agent-harness:graph] to follow references between skills, subagents, personas, and commands.
3. **Run repository validation** -- Use `dotnet agent-harness validate` before relying on new metadata changes.
4. **Check exported inventories** -- Inspect `.dotnet-agent-harness/exports/mcp/manifest.json` and `.dotnet-agent-harness/exports/mcp-report.json` when you need the checked-in MCP bundle view.

## Commands

### Catalog Profile

```bash
/dotnet-agent-harness:profile <catalog-item-id> [--format text|json]
```

Shows item metadata, tags, references, and approximate size.

### Dependency Graph

```bash
/dotnet-agent-harness:graph --item <catalog-item-id> --format mermaid
```

Generates a dependency visualization from catalog references.

### Repository Validation

```bash
dotnet agent-harness validate
```

Runs repo validation checks before or after manifest-related changes.

## Manifest Schema

There are two different manifest shapes in this repository:

- `.dotnet-agent-harness/metadata/skill-manifest.schema.json` is the primary schema for authored toolkit metadata.
- `.dotnet-agent-harness/exports/mcp/manifest.json` is the compact MCP export bundle manifest. It does not use the
  old per-skill object map shape.
- `.dotnet-agent-harness/exports/mcp-report.json` is the richer export report with per-item prompt/resource listings and
  resolved references.

Current MCP bundle manifest example:

```json
{
  "generatedAtUtc": "2026-03-06T11:51:41.7827192+00:00",
  "sourceOfTruth": "../../.rulesync",
  "repoRoot": "../..",
  "platform": "geminicli",
  "kind": "all",
  "promptCount": 47,
  "resourceCount": 240,
  "promptIndexPath": "./prompts/index.json",
  "resourceIndexPath": "./resources/index.json"
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
# Inspect one catalog item
dotnet agent-harness profile dotnet-efcore-patterns --format json

# Output:
# tags, references, source path, and token estimate
```

### Dependency Visualization

```bash
# Graph the reachable references
dotnet agent-harness graph --item dotnet-advisor --depth 2 --format mermaid

# Output:
# Mermaid graph of related catalog items
```

### Validate Repository Metadata

```bash
# Validate the current repository state
dotnet agent-harness validate

# Confirms repo metadata and authored content are internally consistent
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
- `.dotnet-agent-harness/metadata/skill-manifest.schema.json` -- manifest JSON schema
- `.dotnet-agent-harness/exports/mcp/manifest.json` -- checked-in MCP export manifest
- `.dotnet-agent-harness/exports/mcp-report.json` -- checked-in MCP export report

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
