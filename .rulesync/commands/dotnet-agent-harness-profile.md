---
description:
  'Analyze skill performance locally. Measure load time, execution duration, and resource usage without telemetry.'
targets: ['*']
---

# /dotnet-agent-harness:profile

Local performance analysis for skills and commands.

## Usage

```bash
/dotnet-agent-harness:profile [skill-name] [options]
```

## Parameters

- `skill-name`: Skill to profile (omit for full report)
- `--session`: Analyze current session
- `--export`: Export results to file
- `--format`: Output format (`text`, `json`, `html`)

## Examples

```bash
# Profile specific skill
/dotnet-agent-harness:profile dotnet-csharp-coding-standards

# Full session analysis
/dotnet-agent-harness:profile --session

# Export detailed report
/dotnet-agent-harness:profile --session --format html --export profile-report.html

# Compare skill performance
/dotnet-agent-harness:profile dotnet-efcore-patterns --compare dotnet-dapper
```

## Local Analysis Only

**No telemetry is collected. All data stays local.**

Metrics stored in: `.dotnet-agent-harness/analytics/`

## Metrics Captured

- **Load Time**: Time to parse and load skill
- **Execution Duration**: Time to process queries
- **Token Usage**: Estimated tokens consumed (local calculation)
- **Cache Hit Rate**: Local cache effectiveness
- **Cross-reference Resolution**: Time to resolve `skill references` references

## Output

```text
Performance Profile: dotnet-csharp-coding-standards
═══════════════════════════════════════════════════

Load Performance:
  Parse time: 12ms
  Frontmatter validation: 3ms
  Total load: 15ms

Execution Metrics:
  Average query time: 45ms
  Token estimate: 2,400 tokens
  Cache hit rate: 78%

Cross-References:
  References resolved: 12
  Average resolution time: 8ms

Recommendations:
  ✓ Consider caching frequently-accessed skills
  ✓ Preload dependencies for faster response
```

## Session Analysis

Analyzes current session patterns:

- Most-used skills
- Average response times
- Skill combinations frequently used together
- Recommendations for optimization

**All analysis is performed locally. No data leaves your machine.**
