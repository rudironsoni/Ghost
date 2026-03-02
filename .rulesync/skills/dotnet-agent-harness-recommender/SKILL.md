---
name: dotnet-agent-harness-recommender
description:
  'AI-powered skill recommendation engine for dotnet-agent-harness. Analyzes project context and recommends relevant
  skills based on detected frameworks, packages, and patterns. Triggers on: recommend skill, suggest skills, what skill
  should I use, skill suggestion, project analysis recommendation.'
targets: ['*']
tags: ['dotnet', 'skill', 'dotnet-agent-harness', 'recommendation', 'analysis']
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  model: inherit
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
opencode:
  mode: subagent
  tools:
    bash: true
    edit: false
    write: false
copilot:
  tools: ['read', 'search', 'execute']
---

# dotnet-agent-harness-recommender

AI-powered skill recommendation engine that analyzes .NET project context and suggests relevant skills.

## When to Use

Load this skill when:

- Starting work on an unfamiliar project
- Discovering what skills are available for a specific technology
- Understanding which skills apply to detected frameworks
- Getting contextual skill suggestions based on project analysis

## Analysis Process

### 1. Detect Project Context

Analyzes:

- **Project files**: `.csproj`, `.sln`, `global.json`
- **Package references**: NuGet packages and versions
- **Framework targets**: TFM (e.g., `net8.0`, `netstandard2.0`)
- **File patterns**: Source organization and conventions

### 2. Map to Skills

Maps detected technologies to skill tags:

| Detected                            | Suggested Skills                                                     |
| ----------------------------------- | -------------------------------------------------------------------- |
| Microsoft.AspNetCore.\*             | dotnet-minimal-apis, dotnet-api-security, dotnet-middleware-patterns |
| Microsoft.EntityFrameworkCore.\*    | dotnet-efcore-patterns, dotnet-efcore-architecture                   |
| Microsoft.Maui.\*                   | dotnet-maui-development, dotnet-maui-aot                             |
| Microsoft.NET.Sdk.BlazorWebAssembly | dotnet-blazor-components, dotnet-blazor-testing                      |
| Azure.\*                            | dotnet-cloud-specialist, dotnet-resilience                           |
| xunit, nunit, mstest                | dotnet-xunit, dotnet-testing-strategy                                |

### 3. Prioritize Recommendations

Scoring factors:

- **Direct match**: Package name matches skill trigger (100 points)
- **Category match**: Technology category match (50 points)
- **Version compatibility**: Skill supports detected TFM (25 points)
- **Usage frequency**: Popular skills rank higher (10 points)

## Commands

### Get Recommendations

```bash
/dotnet-agent-harness:recommend
```

Analyzes current project and outputs:

```text
Recommended Skills (based on project analysis):

1. dotnet-efcore-patterns (95/100)
   Detected: Microsoft.EntityFrameworkCore 8.0.0
   Why: Core EF patterns for data access layer

2. dotnet-api-security (80/100)
   Detected: Microsoft.AspNetCore.Authentication.JwtBearer
   Why: JWT auth and API security patterns

3. dotnet-resilience (75/100)
   Detected: Polly package
   Why: Retry, circuit breaker patterns
```

### Filter by Category

```bash
/dotnet-agent-harness:recommend --category data
/dotnet-agent-harness:recommend --category testing
/dotnet-agent-harness:recommend --category security
```

### Interactive Mode

```bash
/dotnet-agent-harness:recommend --interactive
```

Guides through:

1. Project type selection
2. Technology stack questions
3. Architecture pattern questions
4. Generates prioritized skill list

## Integration with Hooks

Session start hook automatically runs recommendation when:

- New `.csproj` detected in workspace
- No previous recommendations exist
- Project dependencies changed

See `.rulesync/hooks.json` for implementation.

## Project Pattern Detection

### Web API Projects

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
</Project>
```

Triggers: dotnet-minimal-apis, dotnet-api-versioning, dotnet-openapi

### Class Library

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <TargetFramework>netstandard2.0</TargetFramework>
</Project>
```

Triggers: dotnet-library-api-compat, dotnet-nuget-authoring

### MAUI App

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <UseMaui>true</UseMaui>
</Project>
```

Triggers: dotnet-maui-development, dotnet-maui-aot, dotnet-uno-platform

### Blazor WASM

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" />
</Project>
```

Triggers: dotnet-blazor-components, dotnet-blazor-testing, dotnet-aot-wasm

### Console App

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <OutputType>Exe</OutputType>
</Project>
```

Triggers: dotnet-system-commandline, dotnet-cli-architecture

## Output Format

```json
{
  "project_type": "Web API",
  "detected_frameworks": ["ASP.NET Core 8.0", "EF Core 8.0"],
  "recommendations": [
    {
      "skill": "dotnet-efcore-patterns",
      "confidence": 95,
      "reason": "Microsoft.EntityFrameworkCore detected",
      "priority": "high"
    }
  ],
  "alternatives": [
    {
      "skill": "dotnet-dapper-patterns",
      "reason": "Alternative to EF Core for performance"
    }
  ]
}
```

## Best Practices

- **Start with top 3**: Focus on highest confidence matches
- **Check version compatibility**: Ensure skill supports your TFM
- **Review alternatives**: Consider alternative approaches
- **Document choices**: Note why specific skills were selected

## Local Analysis Only

All analysis happens client-side:

- No external API calls
- No telemetry collection
- No skill usage tracking
- Results cached locally in `.dotnet-agent-harness/recommendations.json`

## References

- [skill:dotnet-project-analysis] -- project context detection
- [skill:dotnet-version-detection] -- TFM and SDK detection
- [skill:dotnet-agent-harness-manifest] -- skill metadata and tags

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
