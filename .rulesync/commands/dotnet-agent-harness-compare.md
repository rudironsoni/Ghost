---
description: 'Compare skill versions, implementations, or effectiveness. Side-by-side analysis with diff output.'
targets: ['*']
---

# /dotnet-agent-harness:compare

Compare skills for version changes or effectiveness analysis.

## Usage

```bash
/dotnet-agent-harness:compare <skill-a> <skill-b> [options]
```

## Parameters

- `skill-a`: First skill to compare
- `skill-b`: Second skill to compare
- `--format`: Output format (`text`, `json`, `html`, `diff`)
- `--focus`: Comparison focus (`content`, `structure`, `effectiveness`)
- `--output`: Save comparison to file

## Examples

```bash
# Compare two different skills
/dotnet-agent-harness:compare dotnet-efcore-patterns dotnet-dapper

# Compare skill versions (requires git)
/dotnet-agent-harness:compare dotnet-csharp-coding-standards --version HEAD~5

# Compare effectiveness metrics
/dotnet-agent-harness:compare dotnet-blazor-patterns dotnet-maui-development --focus effectiveness

# Generate HTML report
/dotnet-agent-harness:compare dotnet-clean-architecture dotnet-vertical-slices --format html --output comparison.html
```

## Comparison Aspects

### Content Comparison

- Description differences
- Trigger phrase overlap
- Knowledge source references
- Code example variations
- Cross-reference patterns

### Structure Comparison

- Frontmatter field differences
- Section organization
- Platform support variations
- Tool requirements

### Effectiveness Comparison (Local Analysis)

- Load time differences
- Token usage comparison
- Response quality metrics
- User satisfaction (if tracked locally)

## Output Examples

### Side-by-Side

```text
┌────────────────────────────────────────────────────────────┐
│ dotnet-efcore-patterns    │ dotnet-dapper                  │
├────────────────────────────────────────────────────────────┤
│ 131 lines                 │ 89 lines                     │
│ EF Core focused           │ Lightweight ORM focused        │
│ 12 code examples          │ 8 code examples                │
│ 5 cross-references        │ 3 cross-references             │
│ All platforms             │ All platforms                  │
└────────────────────────────────────────────────────────────┘
```

### Diff Format

```diff
--- dotnet-efcore-patterns
+++ dotnet-dapper
@@ -15,7 +15,7 @@
- Use EF Core for complex domain models
+ Use Dapper for simple queries
```

## Use Cases

1. **Version Control**: Compare skill before/after edits
2. **Skill Selection**: Choose between similar skills
3. **Consistency Check**: Ensure similar skills follow same patterns
4. **Effectiveness Analysis**: Compare local performance metrics

## Integration

Works with:

- `git diff` for version comparison
- Local analytics for effectiveness metrics
- Skill manifest for structural comparison
