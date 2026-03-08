---
name: dotnet-roslyn-analyzers
description: >-
  Authors Roslyn analyzers. DiagnosticAnalyzer, CodeFixProvider,
  CodeRefactoring, multi-version.
---
# Roslyn Analyzer Hooks

## Overview

Comprehensive Roslyn analyzer integration for dotnet-agent-harness providing real-time code analysis through agent hooks and enforced quality gates via git hooks.

## Architecture

### Layer 1: Agent-Level Hooks (Real-time)
- **Trigger**: Post-edit on .cs files
- **Execution**: Async with 60s timeout
- **Scope**: Single file analysis
- **Output**: JSON with violations + skill recommendations

### Layer 2: Git Hooks (Commit/Push)
- **pre-commit**: Staged files analysis + dotnet format
- **pre-push**: Full solution build + tests + doc validation
- **Enforcement**: Blocks on errors and warnings

## Configuration

### Environment Variables

```bash
# Skip all analyzer checks (default: false, analyzers run by default)
export DOTNET_AGENT_HARNESS_SKIP_ANALYZERS=true

# Configure timeout (default: 60 seconds)
export DOTNET_AGENT_HARNESS_ANALYZER_TIMEOUT=120

# Minimum severity to report (Error/Warning/Info)
export DOTNET_AGENT_HARNESS_ANALYZER_SEVERITY=Warning

# Execution mode (sync/async)
export DOTNET_AGENT_HARNESS_ANALYZER_MODE=async
```

### Project-Level (.editorconfig)

```ini
[*.cs]
# Enable all analyzers
dotnet_analyzer_diagnostic.severity = warning

# Category-based configuration
dotnet_analyzer_diagnostic.category-security.severity = error
dotnet_analyzer_diagnostic.category-performance.severity = warning

# Specific rule overrides
dotnet_diagnostic.CA2007.severity = suggestion  # ConfigureAwait
dotnet_diagnostic.CA1816.severity = error       # GC.SuppressFinalize
```

## Analyzer Packages

### Essential (Always Enabled)
- **Microsoft.CodeAnalysis.NetAnalyzers** - Built-in .NET analyzers
- **Roslynator.Analyzers** - Refactoring and code improvements
- **Roslynator.CodeAnalysis.Analyzers** - Code analysis rules
- **Roslynator.Formatting.Analyzers** - Formatting rules

### Optional
- **StyleCop.Analyzers** - Style consistency (enable/disable via EnableStyleCop)
- **Meziantou.Analyzer** - Performance analyzers (EnableMeziantouAnalyzer)

### CI Only (High Performance Impact)
- **SonarAnalyzer.CSharp** - Security and code smells (+522% build time)

## Analyzer-to-Skill Routing

| Analyzer Code | Skill | Description |
|--------------|-------|-------------|
| CA2007 | dotnet-csharp-async-patterns | Do not directly await a Task |
| CA2008 | dotnet-csharp-async-patterns | TaskScheduler required |
| CA1816 | dotnet-gc-memory | GC.SuppressFinalize |
| CA2000 | dotnet-gc-memory | Dispose objects before losing scope |
| CA1822 | dotnet-performance-patterns | Mark members as static |
| CA1860 | dotnet-performance-patterns | Avoid Enumerable.Any() |
| CA2100 | dotnet-security-owasp | SQL injection prevention |
| CA5350 | dotnet-security-owasp | Weak cryptographic algorithms |
| CA1001 | dotnet-architecture-patterns | IDisposable pattern |
| CA1063 | dotnet-architecture-patterns | Implement IDisposable correctly |

Full mappings in `.rulesync/analyzer-to-skill.json`

## Git Hooks Installation

### Automatic (Recommended)

```bash
# Run setup script
bash .githooks/setup.sh
```

### Manual

```bash
# Copy hooks to .git/hooks/
cp .githooks/pre-commit .git/hooks/
cp .githooks/pre-push .git/hooks/
chmod +x .git/hooks/pre-commit .git/hooks/pre-push
```

### Bypass Hooks

```bash
# Skip analyzer checks for this commit
DOTNET_AGENT_HARNESS_SKIP_ANALYZERS=true git commit -m "message"

# Bypass all hooks (not recommended)
git commit --no-verify -m "message"
```

## Performance Considerations

| Configuration | Build Time Impact | When to Use |
|--------------|-------------------|-------------|
| NetAnalyzers only | +15% | Development (default) |
| + Roslynator | +98% | Development with refactoring |
| + StyleCop | +49% | Style enforcement |
| Full Suite (CI) | +522% | CI/CD pipelines only |

## Troubleshooting

### Analysis Timeouts

Increase timeout:
```bash
export DOTNET_AGENT_HARNESS_ANALYZER_TIMEOUT=120
```

### Memory Issues

For large solutions, disable heavy analyzers:
```xml
<PropertyGroup>
  <RunSonarAnalyzer>false</RunSonarAnalyzer>
</PropertyGroup>
```

### False Positives

Disable specific rules in .editorconfig:
```ini
dotnet_diagnostic.CA1707.severity = none
```

## References

- [Roslyn Analyzer Configuration](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options)
- [Performance Impact Analysis](https://www.meziantou.net/understanding-the-impact-of-roslyn-analyzers-on-the-build-time.htm)
- [Analyzer-to-Skill Mappings](../analyzer-to-skill.json)
- [Git Hooks Setup](../../.githooks/setup.sh)
