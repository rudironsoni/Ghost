---
name: dotnet-slopwatch
category: developer-experience
subcategory: analyzers
description: Runs Slopwatch CLI to detect LLM reward hacking -- disabled tests, suppressed warnings.
license: MIT
targets: ['*']
tags: [foundation, dotnet, skill]
version: '0.0.1'
author: 'dotnet-agent-harness'
invocable: true
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
codexcli:
  short-description: '.NET skill guidance for foundation tasks'
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
copilot: {}
geminicli: {}
antigravity: {}
---

# dotnet-slopwatch

Slopwatch: LLM Anti-Cheat Quality Gate for .NET

Run the `Slopwatch.Cmd` dotnet tool as an automated quality gate after code modifications to detect "slop" -- shortcuts
that make builds/tests pass without fixing real problems.

## Scope

- Slopwatch CLI installation and configuration
- Running quality gate checks (disabled tests, suppressed warnings, empty catches)
- Hook integration and CI/CD pipeline usage
- Detection rule reference

## Out of scope

- Pattern recognition and manual code review for slop -- see [skill:dotnet-agent-gotchas] for Slopwatch anti-patterns

## Prerequisites

- .NET 8.0+ SDK
- `Slopwatch.Cmd` NuGet package (v0.4.0+)

Cross-references: [skill:dotnet-tool-management] for general dotnet tool installation mechanics.

---

## Installation

### Local Tool (Recommended)

Add to `.config/dotnet-tools.json`:

````json

{
  "version": 1,
  "isRoot": true,
  "tools": {
    "slopwatch.cmd": {
      "version": "0.4.0",
      "commands": ["slopwatch"],
      "rollForward": false
    }
  }
}

```text

Then restore:

```bash

dotnet tool restore

```bash

### Global Tool

```bash

dotnet tool install --global Slopwatch.Cmd

```bash

See [skill:dotnet-tool-management] for tool manifest conventions and restore patterns.

### Harness Bootstrap (Recommended for Multi-Agent Repos)

If this repository already uses `dotnet-agent-harness`, prefer the runtime bootstrap path:

```bash
dotnet agent-harness bootstrap \
  --profile platform-native \
  --enable-pack dotnet-intelligence \
  --run-rulesync
```

This installs `Slopwatch.Cmd` into the local tool manifest, writes `.slopwatch/config.json`, and enables advisory
post-edit hook reporting through the generated RuleSync hooks. Hard enforcement still happens in runtime validation.

---

## Usage

### Basic Analysis

```bash

# Analyze current directory for slop
slopwatch analyze

# Analyze specific directory
slopwatch analyze -d ./src

# Strict mode -- fail on warnings too
slopwatch analyze --fail-on warning

# JSON output for tooling integration
slopwatch analyze --output json

# Show performance stats
slopwatch analyze --stats

```json

### First-Time Setup: Establish a Baseline

For existing projects with pre-existing issues, create a baseline so slopwatch only catches **new** slop. The `init` command scans all files and records current findings as the accepted baseline:

```bash

slopwatch init
git add .slopwatch/baseline.json
git commit -m "Add slopwatch baseline"

```bash

### Updating the Baseline (Rare)

Only update when slop is **truly justified** and documented:

```bash

slopwatch analyze --update-baseline

```bash

Valid reasons: third-party library forces a pattern, intentional rate-limiting delay (not test flakiness), generated code that cannot be modified. Always add a code comment explaining the justification.

---

## Configuration

Create `.slopwatch/config.json` to manage suppressions intentionally:

```json

{
  "globalSuppressions": [],
  "suppressions": []
}

```text

Use `slopwatch analyze -d . --fail-on warning` in CI and runtime validation when you want warnings to fail the quality
gate.

---

## Detection Rules

| Rule | Severity | What It Catches |
|------|----------|-----------------|
| SW001 | Error | Disabled tests (`Skip=`, `Ignore`, `#if false`) |
| SW002 | Warning | Warning suppression (`#pragma warning disable`, `SuppressMessage`) |
| SW003 | Error | Empty catch blocks that swallow exceptions |
| SW004 | Warning | Arbitrary delays in tests (`Task.Delay`, `Thread.Sleep`) |
| SW005 | Warning | Project file slop (`NoWarn`, `TreatWarningsAsErrors=false`) |
| SW006 | Warning | CPM bypass (`VersionOverride`, inline `Version` attributes) |

### When Slopwatch Flags an Issue

1. **Understand why** the shortcut was taken
2. **Request a proper fix** -- be specific about what's wrong
3. **Verify the fix** doesn't introduce different slop

```text

# Example output
❌ SW001 [Error]: Disabled test detected
   File: tests/MyApp.Tests/OrderTests.cs:45
   Pattern: [Fact(Skip="Test is flaky")]

```csharp

**Never disable tests to achieve a green build.** Fix the underlying issue.

---

## Claude Code Hook Integration

Add slopwatch as a `PostToolUse` hook to automatically validate every edit. Create or update `.claude/settings.json`:

```json

{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit|MultiEdit",
        "hooks": [
          {
            "type": "command",
            "command": "slopwatch analyze -d . --hook",
            "timeout": 60000
          }
        ]
      }
    ]
  }
}

```text

The `--hook` flag:
- Only analyzes **git dirty files** (fast, even on large repos)
- Outputs errors to stderr in readable format
- Blocks the edit on warnings/errors (exit code 2)
- Claude sees the error and can fix it immediately

When using the toolkit's shared RuleSync hooks, the generated harness keeps this advisory-only by design. Findings are
surfaced back to the agent as context, while `dotnet agent-harness validate --mode repo --run` remains the blocking
quality gate.

This is the pattern used by projects like BrighterCommand/Brighter.

---

## CI/CD Integration

### GitHub Actions

```yaml

jobs:
  slopwatch:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'  # any .NET 8+ SDK works

      - name: Install Slopwatch
        run: dotnet tool install --global Slopwatch.Cmd

      - name: Run Slopwatch
        run: slopwatch analyze -d . --fail-on warning

```text

### Azure Pipelines

```yaml

- task: DotNetCoreCLI@2
  displayName: 'Install Slopwatch'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'install --global Slopwatch.Cmd'

- script: slopwatch analyze -d . --fail-on warning
  displayName: 'Slopwatch Analysis'

```text

---

## Agent Gotchas

- **Do not suppress slopwatch findings.** If slopwatch flags an issue, fix the code -- do not update the baseline or disable the rule without explicit user approval.
- **Run after every code change**, not just at the end. Catching slop early prevents cascading shortcuts.
- **Use `--hook` flag in Claude Code hooks**, not bare `analyze`. The hook flag restricts analysis to dirty files for performance.
- **Baseline is not a wastebasket.** Adding items to the baseline requires documented justification. Never bulk-update baseline to silence warnings.
- **Local tool preferred over global.** Use `.config/dotnet-tools.json` so the version is pinned and reproducible across team members.

---

## Quick Reference

```bash

# First time setup
slopwatch init
git add .slopwatch/baseline.json

# After every code change
slopwatch analyze

# Strict mode (recommended)
slopwatch analyze --fail-on warning

# Hook mode (for Claude Code integration)
slopwatch analyze -d . --hook

# JSON output for tooling
slopwatch analyze --output json

# Update baseline (rare, document why)
slopwatch analyze --update-baseline

```json

---

## References

- [Slopwatch NuGet Package](https://www.nuget.org/packages/Slopwatch.Cmd)
- [skill:dotnet-tool-management] -- dotnet tool installation and manifest conventions
- [skill:dotnet-agent-gotchas] -- manual slop pattern recognition (visual detection counterpart)
- [skill:dotnet-test-quality] -- test coverage and quality measurement
````

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
