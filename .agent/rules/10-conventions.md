---
trigger: glob
globs: .rulesync/**/*
---
# Authoring Conventions

- Keep frontmatter strictly compliant with the RuleSync spec (see `.rulesync/skills/rulesync/file-formats.md`).
- Add platform-specific blocks only when required (`claudecode`, `opencode`, `codexcli`, `copilot`, `geminicli`,
  `factorydroid`, `antigravity`).
- Use `version: "0.0.1"` for newly ported content unless a higher semantic version is intentionally introduced.
- Use ASCII in shell scripts and docs unless a source file already requires Unicode.
- Keep hook scripts advisory-only and return success to avoid blocking user workflows.
- For hook-only targets that do not import skills or commands (for example `factorydroid`), hook text must route through
  generated rules and lightweight reminders instead of requiring unavailable `[skill:...]` or slash-command surfaces.
- Keep commands deterministic; avoid destructive actions unless explicitly requested.
- Keep unsupported content out of `.rulesync/agents/`, `.rulesync/personas/`, and `.rulesync/manifest/`; use
  supported RuleSync surfaces plus repo-owned metadata outside `.rulesync/` instead.

## Frontmatter Portability Contract

### Shared fields (top-level, all platforms read these)

| Field         | Required | Notes                                    |
| ------------- | -------- | ---------------------------------------- |
| `name`        | yes      | Unique identifier for the skill/subagent |
| `description` | yes      | One-line summary                         |
| `targets`     | yes      | Array of target platforms (e.g. `["*"]`) |
| `tags`        | optional | Categorization tags (toolkit convention) |
| `version`     | optional | Semantic version string                  |
| `author`      | optional | Toolkit or author name                   |
| `license`     | optional | License identifier                       |

### Banned at top-level

| Field            | Reason                                                                  |
| ---------------- | ----------------------------------------------------------------------- |
| `tools`          | Platform-specific — belongs inside each platform block in native format |
| `model`          | Platform-specific (Claude: `sonnet`/`inherit`, OpenCode: `provider/id`) |
| `user-invocable` | Not a RuleSync concept — do not use                                     |
| `capabilities`   | Informational only — belongs in body text, not frontmatter              |
| `context`        | Removed entirely                                                        |

### Platform-specific blocks

````yaml

claudecode:
  model: optional # sonnet | opus | haiku | inherit
  allowed-tools: optional # canonical names: Read, Grep, Glob, Bash, Edit, Write

opencode:
  mode: required # primary | subagent
  tools: optional # boolean map — explicitly set true/false for bash, edit, write
  permission: optional # per-tool permission overrides (e.g. bash: { "git diff": allow })

copilot:
  tools:
    optional # platform-mapped names (see table below)
    # agent/runSubagent is included automatically

codexcli:
  sandbox_mode: optional # "read-only" for read-only agents; omit to inherit parent sandbox
  short-description: optional

```text

### Tool configuration by platform

**Claude Code** uses an allow-list (`allowed-tools`): only listed canonical tool names are available.

**OpenCode** uses a boolean map (`tools`): explicitly set `true` or `false` for `bash`, `edit`, and `write`. Always
declare all three for clarity.

**Copilot** uses a mapped allow-list (`tools`): only listed platform-mapped names are available. `agent/runSubagent` is
included automatically.

**Codex CLI** uses `sandbox_mode` for permission control. Only read-only agents set `sandbox_mode: "read-only"`.
Standard and Full agents omit this field to inherit the parent session's sandbox policy.

### Tool name mapping (canonical → Copilot)

| Canonical | Copilot   |
| --------- | --------- |
| `Read`    | `read`    |
| `Grep`    | `search`  |
| `Glob`    | `search`  |
| `Bash`    | `execute` |
| `Edit`    | `edit`    |
| `Write`   | `edit`    |

## Command Portability Contract

### Portability Classification

Commands must declare their portability classification in frontmatter:

| Classification | Description | Best For |
|---------------|-------------|----------|
| `universal` | Works on all targets with full functionality | Bootstrap, simple queries |
| `claude-opencode` | Requires rich tool surface | Complex multi-step operations |
| `copilot-gemini` | Works with mapped tool names | Metadata, profile, recommendations |
| `codex-global` | Global mode only | Read-only analysis |
| `antigravity` | Workflow-triggered | Quality gates, hooks |

### Flattening Risk

Commands must declare flattening risk for Copilot (which flattens metadata):

| Risk Level | When to Use | Mitigation |
|------------|-------------|------------|
| `high` | Complex multi-step, WebSearch, triggers | Document in body, use simple sequences |
| `medium` | Multiple tools, file operations | Minimize tool list, clear documentation |
| `low` | Simple queries, single tool | Safe, no special handling needed |

### Simulated Behavior

Commands must declare if behavior is simulated for Factory Droid:

| Value | Meaning |
|-------|---------|
| `true` | Command intent delivered via rules/hooks |
| `false` | Native command invocation available |

### Example Command Frontmatter

```yaml
---
name: dotnet-agent-harness-search
description: 'Search skills by keyword'
targets: ['*']
portability: claude-opencode
flattening-risk: medium
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Search skills by keyword'
codexcli:
  sandbox_mode: 'read-only'
---
```

## Naming Collision Handling

### RuleSync Collision Resolution

RuleSync handles command name collisions as follows:

1. **Last Write Wins**: When multiple sources define the same command name, the last source in the `sources` array wins.
2. **Prefix Namespacing**: Use prefixes to avoid collisions: `<toolkit>-<command>`
3. **Directory Structure**: Commands in subdirectories maintain their full path in generated output

### Copilot Flattening

Copilot flattens command metadata:

| Preserved | Lost |
|-----------|------|
| Name, description | Complex tool sequences |
| Basic tool list | Trigger metadata |
| Body content | Platform-specific blocks |

**Mitigation:**
- Use descriptive names that encode intent
- Document complex behavior in body
- Keep tool lists minimal
- Use `copilot.description` override for clarity

### Naming Conventions to Avoid Flattening

1. **Prefix Convention**: Always use `<toolkit>-` prefix
   - Good: `dotnet-agent-harness-search`
   - Bad: `search`

2. **Verb-Noun Pattern**: Action-oriented naming
   - Good: `generate`, `analyze`, `validate`
   - Bad: `helper`, `utils`, `misc`

3. **Kebab-Case**: Lowercase with hyphens
   - Good: `deep-wiki-generate`
   - Bad: `deepWikiGenerate`, `deep_wiki_generate`

4. **No Duplicate Names**: Each command must be unique
   - Check: `commands/` directory for existing names
   - Check: Generated output for conflicts

### Subdirectories vs Flat Structure

| Structure | Use When | Example |
|-----------|----------|---------|
| **Flat** | Few commands (<20), simple toolkit | `commands/init.md` |
| **Grouped** | Many commands, clear categories | `commands/deep-wiki/generate.md` |

**Current Convention**: Use flat structure with prefixes:
- `dotnet-agent-harness-*.md`: Core runtime commands
- `deep-wiki-*.md`: Wiki generation commands

This avoids directory flattening issues while maintaining clear organization.

### Subagent tool profiles

This toolkit uses three standard profiles:

| Profile   | Claude Code `allowed-tools`                     | OpenCode `tools`                             | Copilot `tools`                         | Codex CLI `sandbox_mode` |
| --------- | ----------------------------------------------- | -------------------------------------------- | --------------------------------------- | ------------------------ |
| Read-only | `Read`, `Grep`, `Glob`                          | `bash: false`, `edit: false`, `write: false` | `["read", "search"]`                    | `"read-only"`            |
| Standard  | `Read`, `Grep`, `Glob`, `Bash`                  | `bash: true`, `edit: false`, `write: false`  | `["read", "search", "execute"]`         | _(inherits parent)_      |
| Full      | `Read`, `Grep`, `Glob`, `Bash`, `Edit`, `Write` | `bash: true`, `edit: true`, `write: true`    | `["read", "search", "execute", "edit"]` | _(inherits parent)_      |
```

---

## Skill Taxonomy Conventions

Skills MUST include taxonomy classification in their frontmatter for discoverability and routing.

### Required Taxonomy Fields

| Field         | Required | Values                                                | Purpose                    |
| ------------- | -------- | ----------------------------------------------------- | -------------------------- |
| `category`    | yes      | fundamentals, testing, architecture, web, data, performance, security, devops, platforms, tooling | Top-level classification   |
| `subcategory` | yes      | See TAXONOMY.md for valid subcategories per category  | Secondary classification   |
| `complexity`  | yes      | beginner, intermediate, advanced                    | Difficulty/audience level  |
| `tags`        | yes      | Array including complexity + domain tags            | Discovery and filtering    |

### Category Definitions

| Category     | Code  | Description                          | ~Count |
| ------------ | ----- | ------------------------------------ | ------ |
| fundamentals | FND   | Core C#/.NET language and runtime    | ~25    |
| testing      | TST   | Testing methodology and frameworks   | ~35    |
| architecture | ARC   | Design patterns and system design    | ~15    |
| web          | WEB   | ASP.NET Core and web frameworks      | ~20    |
| data         | DAT   | Data access, EF Core, messaging      | ~15    |
| performance  | PERF  | Optimization and benchmarking        | ~10    |
| security     | SEC   | Security, OWASP, cryptography        | ~10    |
| devops       | DEV   | CI/CD, containers, deployment        | ~20    |
| platforms    | PLAT  | UI frameworks (MAUI, WPF, etc.)    | ~20    |
| tooling      | TOOL  | CLI, analyzers, MSBuild              | ~15    |

### Complexity Levels

| Level        | Target Audience              | Characteristics                          |
| ------------ | ---------------------------- | ------------------------------------------ |
| beginner     | New to .NET                  | Foundational concepts, basic patterns      |
| intermediate | Working developers           | Practical application, integration         |
| advanced     | Senior developers/architects | Expert patterns, edge cases, optimization  |

### Example Skill Frontmatter

```yaml
---
name: dotnet-testing-unit-test-fundamentals
description: FIRST principles, 3A Pattern, and unit testing fundamentals
license: MIT
targets: ['*']
category: testing
subcategory: fundamentals
complexity: beginner
tags:
  - dotnet
  - testing
  - xunit
  - beginner
version: '1.0.0'
author: 'dotnet-agent-harness'
related_skills:
  - dotnet-testing-test-naming-conventions
  - dotnet-testing-xunit-project-setup
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
---
```

### Discovering Skills by Category

1. **Browse INDEX.md** - Complete categorized listing at `.rulesync/skills/INDEX.md`
2. **Check TAXONOMY.md** - Full taxonomy schema at `.rulesync/skills/TAXONOMY.md`
3. **Use meta-skills** - Load category overviews like `dotnet-testing`, `dotnet-architecture`
4. **Filter by complexity** - Look for beginner/intermediate/advanced tags
5. **Follow related_skills** - Cross-references in skill frontmatter

### Classifying New Skills

When creating a new skill:

1. **Determine category** based on primary domain
2. **Select subcategory** from TAXONOMY.md definitions
3. **Assess complexity** honestly - err toward lower if uncertain
4. **Add appropriate tags** - include complexity + 2-3 domain tags
5. **Cross-reference** related skills in `related_skills` field
6. **Update INDEX.md** with the new skill entry
7. **Update meta-skills** if this is a major addition

### Target Platform Tags

Skills targeting specific platforms should include platform tags:

```yaml
tags:
  - dotnet
  - testing
  - claudecode    # If Claude Code specific
  - beginner
```

Use `['*']` in `targets` for universal compatibility (most skills).

### Skill Index Maintenance

The skill index at `.rulesync/skills/INDEX.md` must be kept current:

- **Add new skills** to alphabetical and category listings
- **Update counts** in category summary tables
- **Verify cross-references** are accurate
- **Regenerate** from source when bulk changes occur`
