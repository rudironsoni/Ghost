---
# ═══════════════════════════════════════════════════════════════════════════════
# SKILL FRONTMATTER TEMPLATE
# Copy this template and customize for your new skill
# ═══════════════════════════════════════════════════════════════════════════════

# ───────────────────────────────────────────────────────────────────────────────
# CORE IDENTIFICATION (Required)
# ───────────────────────────────────────────────────────────────────────────────

# name: Unique skill identifier in kebab-case (lowercase, hyphens)
# Example: dotnet-csharp-async-patterns, dotnet-testing-unit-test-fundamentals
name: your-skill-name

# description: > (folded YAML style for multi-line)
# Concise summary with trigger keywords. First line should be actionable.
# Include "Use when..." or "Load when..." pattern for auto-triggering.
description: >
  Brief description of what this skill does. Load when users ask about X, Y, Z.
  Keywords: keyword1, keyword2, keyword3, trigger-word, another-keyword

# ───────────────────────────────────────────────────────────────────────────────
# INVOCATION CONTROL
# ───────────────────────────────────────────────────────────────────────────────

# invocable: Can this skill be directly invoked by users? (true/false)
# Most skills: true (user can /skill <name>)
# Navigation/meta skills: false (auto-triggered only)
invocable: true

# ───────────────────────────────────────────────────────────────────────────────
# VERSION & LICENSING
# ───────────────────────────────────────────────────────────────────────────────

# version: Semantic versioning (MAJOR.MINOR.PATCH)
# MAJOR: Breaking changes | MINOR: New features | PATCH: Fixes/docs
version: '1.0.0'

# license: SPDX identifier or full name
# Most skills: MIT
license: MIT

# author: Attribution (optional)
author: 'dotnet-agent-harness'

# ───────────────────────────────────────────────────────────────────────────────
# TARGET PLATFORMS (Required)
# ───────────────────────────────────────────────────────────────────────────────

# targets: Array of supported AI platforms
# Use ['*'] for universal compatibility (most common)
# Specific: ['claudecode', 'opencode', 'copilot', 'geminicli', 'codexcli', 'antigravity', 'factorydroid']
targets:
  - '*'

# ───────────────────────────────────────────────────────────────────────────────
# TAXONOMY CLASSIFICATION (Required)
# ───────────────────────────────────────────────────────────────────────────────

# category: Top-level domain (10 standardized categories)
#   fundamentals   - Core C#/.NET language and runtime
#   testing        - Testing methodology and frameworks
#   architecture   - Design patterns and system design
#   web            - ASP.NET Core and web frameworks
#   data           - Data access, EF Core, messaging
#   performance    - Optimization and benchmarking
#   security       - Security, OWASP, cryptography
#   devops         - CI/CD, containers, deployment
#   platforms      - UI frameworks (MAUI, WPF, Blazor, etc.)
#   tooling        - CLI, analyzers, MSBuild, documentation
category: fundamentals

# subcategory: Secondary classification (see TAXONOMY.md for valid values)
# Examples per category:
#   fundamentals: coding-standards, language-patterns, dependency-injection, configuration
#   testing: fundamentals, assertions, mocking, test-data, integration, frameworks
#   architecture: patterns, domain-modeling, messaging, resilience, caching
#   web: blazor, api-design, security, minimal-apis, validation
#   data: ef-core, data-access, serialization
#   performance: aot, benchmarking, profiling, memory
#   security: owasp, auth, crypto, secrets
#   devops: ci-cd, containers, github-actions, azure-devops, release
#   platforms: maui, wpf, winui, winforms, uno, blazor
#   tooling: cli, analyzers, msbuild, nuget, project, docs
subcategory: language-patterns

# complexity: Skill difficulty level
#   beginner     - New to .NET (foundational concepts)
#   intermediate - Working developers (practical application)
#   advanced     - Senior developers/architects (expert patterns)
complexity: intermediate

# ───────────────────────────────────────────────────────────────────────────────
# DISCOVERY & TAGGING
# ───────────────────────────────────────────────────────────────────────────────

# tags: Array for discovery and filtering
# Include: complexity + domain + technology tags
tags:
  - dotnet                    # Primary framework
  - csharp                    # Language (if applicable)
  - category-name             # Match your category
  - beginner|intermediate|advanced  # Match complexity
  - specific-tech             # e.g., xunit, ef-core, blazor
  - concept-tag               # e.g., async, di, testing

# keywords: Extended trigger words for auto-suggestion
# Include synonyms, related terms, common misspellings
keywords:
  - primary-concept
  - related-term
  - synonym-phrase
  - common-question
  - alternative-keyword

# ───────────────────────────────────────────────────────────────────────────────
# AUTO-TRIGGER CONDITIONS
# ───────────────────────────────────────────────────────────────────────────────

# triggers: When should this skill be auto-suggested?
# Use natural language patterns matching user queries
triggers:
  - 'when user asks about X'
  - 'when user mentions Y keyword'
  - 'when user needs help with Z'
  - 'when implementing specific-pattern'

# ───────────────────────────────────────────────────────────────────────────────
# SKILL RELATIONSHIPS
# ───────────────────────────────────────────────────────────────────────────────

# depends_on: Prerequisite skills that should be understood first
# These are "hard dependencies" - load these before using this skill
depends_on:
  - skill: dotnet-csharp-coding-standards
    reason: 'Core coding standards required for consistency'
  - skill: dotnet-fundamentals
    reason: 'Base .NET knowledge assumed'

# seealso: Related skills with relationship type
# These are "soft relationships" - cross-references for exploration
seealso:
  - skill: dotnet-related-skill-1
    relationship: complements    # Works well together
    description: 'Use together for full coverage'
  - skill: dotnet-related-skill-2
    relationship: prerequisite  # Should learn first
    description: 'Foundational concepts needed'
  - skill: dotnet-related-skill-3
    relationship: alternative   # Different approach
    description: 'Alternative solution for similar problem'
  - skill: dotnet-related-skill-4
    relationship: extends       # Builds upon
    description: 'Advanced patterns built on these basics'

# ───────────────────────────────────────────────────────────────────────────────
# LIFECYCLE MANAGEMENT
# ───────────────────────────────────────────────────────────────────────────────

# status: Skill maturity level
#   stable    - Production ready, fully reviewed
#   beta      - New skill, under review, may have changes
#   deprecated - Being phased out, use alternative
status: stable

# last_reviewed: Date of last comprehensive review (YYYY-MM-DD)
last_reviewed: '2026-03-07'

# review_cycle: How often should this skill be reviewed?
# Examples: quarterly, semi-annually, annually, on-major-release
review_cycle: semi-annually

# ───────────────────────────────────────────────────────────────────────────────
# PLATFORM-SPECIFIC CONFIGURATION
# ───────────────────────────────────────────────────────────────────────────────
# Only include blocks for platforms you need to customize
# Omit to inherit defaults from parent/caller

# ─── Claude Code ──────────────────────────────────────────────────────────────
claudecode:
  # model: Optional model override (sonnet | opus | haiku | inherit)
  # Omit to inherit from session
  model: inherit
  
  # allowed-tools: Canonical tool names this skill can use
  # Read-only: ['Read', 'Grep', 'Glob']
  # Standard: ['Read', 'Grep', 'Glob', 'Bash']
  # Full: ['Read', 'Grep', 'Glob', 'Bash', 'Edit', 'Write']
  allowed-tools:
    - Read
    - Grep
    - Glob
    - Bash
    - Edit
    - Write

# ─── OpenCode ─────────────────────────────────────────────────────────────────
opencode:
  # mode: Required - primary (Tab rotation) or subagent (@mention only)
  mode: primary
  
  # tools: Boolean map for tool permissions
  # Explicitly declare all three for clarity
  tools:
    bash: true   # Can execute shell commands
    edit: true   # Can modify existing files
    write: true  # Can create new files
  
  # permission: Optional per-tool permission overrides
  permission:
    bash:
      'git status': allow
      'dotnet build': allow
      'rm -rf': deny    # Example: block destructive commands

# ─── Copilot ─────────────────────────────────────────────────────────────────
copilot:
  # tools: Platform-mapped tool names
  # Canonical → Copilot: Read=read, Grep/Glob=search, Bash=execute, Edit/Write=edit
  tools:
    - read
    - search
    - execute
    - edit
  
  # description: Optional override for Copilot display
  description: 'Custom description for Copilot context menu'

# ─── Codex CLI ───────────────────────────────────────────────────────────────
codexcli:
  # sandbox_mode: "read-only" for read-only agents, omit for full access
  sandbox_mode: 'read-only'
  
  # short-description: Brief context for compact displays
  short-description: 'Brief one-liner for Codex'

# ─── Gemini CLI ──────────────────────────────────────────────────────────────
geminicli:
  # Gemini inherits shared rules + hooks
  # Add platform-specific overrides here
  hooks:
    post-edit: 'dotnet format'

# ─── Factory Droid ───────────────────────────────────────────────────────────
factorydroid:
  # Factory Droid uses rules+hooks delivery
  # Skills delivered via generated rules
  rules: true
  hooks: true

# ─── Antigravity ─────────────────────────────────────────────────────────────
antigravity:
  # trigger: How this skill is activated
  #   always_on   - Available in all sessions
  #   glob        - Activated by file patterns
  #   manual      - User must explicitly invoke
  trigger: always_on
  
  # globs: File patterns that activate this skill (if trigger: glob)
  globs:
    - '**/*.csproj'
    - '**/*.sln'

---

# ═══════════════════════════════════════════════════════════════════════════════
# SKILL CONTENT BEGINS BELOW
# ═══════════════════════════════════════════════════════════════════════════════

# SKILL_NAME

One-line summary of what this skill covers.

## Overview

Brief overview paragraph explaining the skill's purpose and scope.

- What problem does this skill solve?
- Who is the target audience?
- What will they learn?

## When to Use This Skill

Load this skill when:

- **Scenario 1**: Description of when to use this skill
- **Scenario 2**: Another common use case
- **Scenario 3**: Specific situation requiring this expertise

### Decision Tree

```
User needs help with...
├── Option A → Use this skill
├── Option B → Use [skill:related-skill-1]
└── Option C → Use [skill:related-skill-2]
```

## Prerequisites

Before using this skill, ensure you have:

1. **Required Knowledge**
   - Prerequisite concept 1
   - Prerequisite concept 2

2. **Required Tools**
   - Tool version requirements
   - SDK/framework versions

3. **Optional but Recommended**
   - [skill:prerequisite-skill-1] - Why it's helpful
   - [skill:prerequisite-skill-2] - What it provides

## Key Concepts

### Concept 1: Concept Name

Explanation of the first key concept.

```csharp
// Code example demonstrating the concept
public class Example
{
    public void DemonstrateConcept()
    {
        // Implementation here
    }
}
```

### Concept 2: Another Concept

Explanation of the second key concept.

| Option | When to Use | Example |
|--------|-------------|---------|
| Option A | Use when... | `code snippet` |
| Option B | Use when... | `code snippet` |

### Concept 3: Advanced Topic

More complex concept for intermediate/advanced skills.

> **Note**: Important clarification or warning about edge cases.

## Usage Examples

### Example 1: Basic Usage

```csharp
// Simple, complete example
public class BasicExample
{
    public void BasicUsage()
    {
        // Step 1: Setup
        var service = new MyService();
        
        // Step 2: Action
        var result = service.DoWork();
        
        // Step 3: Verification
        Console.WriteLine(result);
    }
}
```

**Key Points:**
- Important detail about the example
- Another thing to notice
- Common pitfall to avoid

### Example 2: Intermediate Pattern

```csharp
// More complex example showing patterns
public class IntermediateExample
{
    private readonly IService _service;
    
    public IntermediateExample(IService service)
    {
        _service = service;
    }
    
    public async Task<Result> ProcessAsync(CancellationToken ct)
    {
        // Implementation with best practices
        var data = await _service.FetchAsync(ct);
        return Transform(data);
    }
}
```

### Example 3: Advanced Scenario

```csharp
// Expert-level example with edge cases
public class AdvancedExample
{
    // Advanced pattern implementation
}
```

## Common Patterns

### Pattern 1: Pattern Name

Description of when and why to use this pattern.

```csharp
// Pattern implementation
```

**Benefits:**
- Benefit 1
- Benefit 2

### Pattern 2: Anti-Pattern to Avoid

❌ **Don't do this:**

```csharp
// Bad code example
```

✅ **Do this instead:**

```csharp
// Good code example
```

## Platform-Specific Considerations

### Claude Code

- Use `serena_find_symbol` for navigation when available
- Follow tool restrictions from frontmatter
- Leverage full tool surface for complex operations

### OpenCode

- Tab rotation triggers: mention primary keywords early
- @mention routing for subagent skills
- Tool permissions enforced by frontmatter

### Copilot

- Tool names are mapped (search instead of Grep/Glob)
- `agent/runSubagent` included automatically
- Keep tool sequences simple for flattening safety

### Codex CLI

- Respect `sandbox_mode: "read-only"` if set
- Use for analysis and recommendations
- More limited tool surface than Claude/OpenCode

## Troubleshooting

### Issue 1: Common Error

**Symptom:** Description of what goes wrong

**Cause:** Explanation of why it happens

**Solution:**

```csharp
// Fixed code
```

### Issue 2: Performance Problem

**Symptom:** Slow behavior or resource issue

**Solution:**
1. Step to diagnose
2. Step to fix
3. Verification step

### Issue 3: Integration Conflict

**Symptom:** Compatibility issue with other tools

**Workaround:**

```csharp
// Alternative approach
```

## References

- [Microsoft Documentation](https://learn.microsoft.com/...) - Official docs
- [Related Specification](https://...) - Technical spec
- [Blog Post](https://...) - Practical guide

## See Also

- **[skill:related-skill-1]** - Complementary skill for related topic
- **[skill:related-skill-2]** - Alternative approach to similar problem
- **[skill:prerequisite-skill]** - Foundation concepts

---

## Skill Maintenance

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-03-07 | 1.0.0 | Initial skill creation | Your Name |
| YYYY-MM-DD | X.Y.Z | Description of changes | Your Name |

---

## Attribution

- Created for: dotnet-agent-harness
- Based on: [Original source if applicable]
- License: MIT
