---
root: false
targets: ["*"]
description: "dotnet-agent-harness authoring conventions for skills, subagents, commands, and hooks"
globs: [".rulesync/**/*"]
antigravity:
  trigger: glob
  globs: [".rulesync/**/*"]
---

# Authoring Conventions

- Keep frontmatter strictly compliant with the RuleSync spec (see `.rulesync/skills/rulesync/file-formats.md`).
- Add platform-specific blocks only when required (`claudecode`, `opencode`, `codexcli`, `copilot`, `geminicli`).
- Use `version: "0.0.1"` for newly ported content unless a higher semantic version is intentionally introduced.
- Use ASCII in shell scripts and docs unless a source file already requires Unicode.
- Keep hook scripts advisory-only and return success to avoid blocking user workflows.
- Keep commands deterministic; avoid destructive actions unless explicitly requested.

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

| Field            | Reason                                                                     |
| ---------------- | -------------------------------------------------------------------------- |
| `tools`          | Platform-specific — belongs inside each platform block in native format    |
| `model`          | Platform-specific (Claude: `sonnet`/`inherit`, OpenCode: `provider/id`)    |
| `user-invocable` | Not a RuleSync concept — do not use                                        |
| `capabilities`   | Informational only — belongs in body text, not frontmatter                 |
| `context`        | Removed entirely                                                           |

### Platform-specific blocks

```yaml
claudecode:
  model: optional           # sonnet | opus | haiku | inherit
  allowed-tools: optional   # canonical names: Read, Grep, Glob, Bash, Edit, Write

opencode:
  mode: required            # primary | subagent
  tools: optional           # boolean map — explicitly set true/false for bash, edit, write
  permission: optional      # per-tool permission overrides (e.g. bash: { "git diff": allow })

copilot:
  tools: optional           # platform-mapped names (see table below)
                            # agent/runSubagent is included automatically

codexcli:
  sandbox_mode: optional    # "read-only" for read-only agents; omit to inherit parent sandbox
  short-description: optional
```

### Tool configuration by platform

**Claude Code** uses an allow-list (`allowed-tools`): only listed canonical tool names are available.

**OpenCode** uses a boolean map (`tools`): explicitly set `true` or `false` for `bash`, `edit`, and `write`. Always declare all three for clarity.

**Copilot** uses a mapped allow-list (`tools`): only listed platform-mapped names are available. `agent/runSubagent` is included automatically.

**Codex CLI** uses `sandbox_mode` for permission control. Only read-only agents set `sandbox_mode: "read-only"`. Standard and Full agents omit this field to inherit the parent session's sandbox policy.

### Tool name mapping (canonical → Copilot)

| Canonical | Copilot   |
| --------- | --------- |
| `Read`    | `read`    |
| `Grep`    | `search`  |
| `Glob`    | `search`  |
| `Bash`    | `execute` |
| `Edit`    | `edit`    |
| `Write`   | `edit`    |

### Subagent tool profiles

This toolkit uses three standard profiles:

| Profile   | Claude Code `allowed-tools`                      | OpenCode `tools`                                           | Copilot `tools`                        | Codex CLI `sandbox_mode`  |
| --------- | ------------------------------------------------ | ---------------------------------------------------------- | -------------------------------------- | ------------------------- |
| Read-only | `Read`, `Grep`, `Glob`                           | `bash: false`, `edit: false`, `write: false`               | `["read", "search"]`                   | `"read-only"`             |
| Standard  | `Read`, `Grep`, `Glob`, `Bash`                   | `bash: true`, `edit: false`, `write: false`                | `["read", "search", "execute"]`        | *(inherits parent)*       |
| Full      | `Read`, `Grep`, `Glob`, `Bash`, `Edit`, `Write`  | `bash: true`, `edit: true`, `write: true`                  | `["read", "search", "execute", "edit"]`| *(inherits parent)*       |
