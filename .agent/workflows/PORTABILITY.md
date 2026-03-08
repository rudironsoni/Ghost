---
description: >-
  Analysis of 27 commands for target portability classification across all
  supported platforms
trigger: /PORTABILITY
turbo: true
---
# Workflow: /PORTABILITY

# Command Portability Analysis

> Analysis of 27 commands for target portability classification
> Last reviewed: 2026-03-07

## Portability Classification System

| Classification | Description | Targets Supported |
|--------------|-------------|-------------------|
| `universal` | Works on all targets with full functionality | All 7 targets |
| `claude-opencode` | Requires rich tool surface (Read, Grep, Glob, Bash, Edit) | Claude Code, OpenCode |
| `copilot-gemini` | Works with mapped tool names (read, search, execute, edit) | Copilot, Gemini CLI |
| `codex-global` | Global mode only, read-only or simple operations | Codex CLI |
| `antigravity` | Workflow-triggered, rules-based | Antigravity |

## Command Classification Summary

### 27 Commands Analyzed

| Command | Classification | Flattening Risk | Simulated | Notes |
|---------|---------------|-----------------|-----------|-------|
| `dotnet-agent-harness-bootstrap` | universal | low | true | Core bootstrap command |
| `dotnet-agent-harness-search` | claude-opencode | medium | true | Uses Glob/Grep extensively |
| `dotnet-agent-harness-graph` | claude-opencode | medium | true | Complex file operations |
| `dotnet-agent-harness-test` | claude-opencode | medium | true | Requires Bash for test execution |
| `dotnet-agent-harness-compare` | claude-opencode | medium | true | File comparison operations |
| `dotnet-agent-harness-compare-prompts` | claude-opencode | medium | true | Prompt diff operations |
| `dotnet-agent-harness-incident` | claude-opencode | medium | true | Incident file management |
| `dotnet-agent-harness-metadata` | copilot-gemini | low | true | Simple metadata queries |
| `dotnet-agent-harness-profile` | copilot-gemini | low | true | Profile generation |
| `dotnet-agent-harness-recommend` | copilot-gemini | low | true | Skill recommendations |
| `dotnet-agent-harness-export-mcp` | copilot-gemini | low | true | MCP export operations |
| `dotnet-agent-harness-prepare-message` | claude-opencode | medium | true | Complex message assembly |
| `init-project` | claude-opencode | medium | true | Multi-step initialization |
| `dotnet-slopwatch` | antigravity | low | false | Has Antigravity trigger |
| `deep-wiki-generate` | claude-opencode | medium | true | Multi-file generation |
| `deep-wiki-build` | copilot-gemini | low | true | Build operations |
| `deep-wiki-deploy` | copilot-gemini | low | true | Deployment workflow |
| `deep-wiki-page` | claude-opencode | medium | true | Page generation with diagrams |
| `deep-wiki-ask` | claude-opencode | medium | true | Q&A with search |
| `deep-wiki-research` | claude-opencode | high | true | WebSearch + deep analysis |
| `deep-wiki-catalogue` | copilot-gemini | low | true | Structure generation |
| `deep-wiki-onboard` | copilot-gemini | low | true | Onboarding guides |
| `deep-wiki-changelog` | copilot-gemini | low | true | Git history analysis |
| `deep-wiki-agents` | copilot-gemini | low | true | AGENTS.md generation |
| `deep-wiki-ado` | copilot-gemini | low | true | ADO conversion |
| `deep-wiki-llms` | copilot-gemini | low | true | LLM docs generation |
| `deep-wiki-crisp` | copilot-gemini | low | true | Quick wiki generation |

## Commands by Target Support

### Full Support (All Features)

**Claude Code**: All 27 commands with native tool mapping
**OpenCode**: All 27 commands with native tool mapping

### Good Support (Most Features)

**Copilot**: 27 commands via tool name mapping
- `read` → Read
- `search` → Grep/Glob
- `execute` → Bash
- `edit` → Edit/Write

**Gemini CLI**: 27 commands via similar mapping
- Note: `agent/runSubagent` is included automatically

### Limited Support

**Codex CLI**: 27 commands in read-only mode by default
- Commands requiring file writes need explicit sandbox override
- Best for: `search`, `compare`, `recommend`, `profile`

**Antigravity**: 2 commands with explicit triggers
- `dotnet-slopwatch`: Has `antigravity.trigger: '/dotnet-slopwatch'`
- `init-project`: Has `antigravity.trigger: '/init-project'`
- Other commands available via rules/hooks only

**Factory Droid**: 0 commands directly invocable
- Consumes generated rules and hooks only
- All commands simulated: true

## Flattening Risk Analysis

### High Risk (Copilot)

Commands that lose metadata when flattened:

| Command | Risk | Mitigation |
|---------|------|------------|
| `deep-wiki-research` | High | Uses WebSearch which maps to `search` |
| Commands with `antigravity` blocks | Medium | Trigger metadata lost |
| Complex multi-step commands | Medium | Step sequencing hints lost |

### Medium Risk

Commands with extensive tool lists:
- `dotnet-agent-harness-search`
- `dotnet-agent-harness-graph`
- `init-project`

### Low Risk

Simple commands with minimal tool requirements:
- `dotnet-agent-harness-bootstrap`
- `dotnet-agent-harness-metadata`
- `deep-wiki-build`
- `deep-wiki-deploy`

## Simulated vs Native Behavior

### Factory Droid Simulation

All commands are **simulated** for Factory Droid:

| Command | Simulation Method |
|---------|------------------|
| All 27 | Via generated rules and hooks |

Factory Droid does not support:
- Direct command invocation
- MCP servers
- Subagents

Content is delivered via:
- Generated rules with command guidance
- Hook text for advisory reminders

## Target-Specific Limitations

### Claude Code
- Full feature support
- All tools available: Read, Grep, Glob, Bash, Edit, Write
- Subagent support via `@`

### OpenCode
- Full feature support
- Tab cycles primary agents
- `@mention` for subagents
- Tool permissions via `tools:` map

### Copilot
- Tool name mapping required
- `agent/runSubagent` automatic
- Limited to: read, search, execute, edit

### Gemini CLI
- Similar to Copilot
- Hook support available
- Subagents not consumed directly

### Codex CLI
- Read-only by default for safety
- Sandbox mode: "read-only"
- Commands/hooks not native surfaces

### Antigravity
- Workflow-triggered commands only
- Global rules injection
- No MCP or subagent support

### Factory Droid
- Rules + hooks only delivery
- No direct command invocation
- All behavior simulated

## Naming Collision Analysis

### Current State: No Collisions

All 27 commands have unique names following the pattern:
- `dotnet-agent-harness-<verb>`: 13 commands
- `deep-wiki-<noun>`: 14 commands

### Collision Prevention Rules

1. **Prefix Convention**: Use `dotnet-agent-harness-` or `deep-wiki-` prefixes
2. **Kebab-case**: All lowercase with hyphens
3. **Verb-noun**: Action-oriented naming
4. **No duplicates**: Each command has unique function

## Recommendations

### For Universal Commands
- Keep `targets: ['*']`
- Use standard tool blocks
- Avoid target-specific dependencies

### For Platform-Specific Commands
- Add `portability:` field to frontmatter
- Document limitations in body
- Provide fallback behavior

### For Copilot Optimization
- Minimize tool list to essentials
- Use simple tool sequences
- Avoid complex nested operations

### For Factory Droid
- Ensure rules/hooks cover command intent
- Document simulated behavior
- Provide clear workarounds

## Validation Checklist

- [x] All 27 commands analyzed
- [x] Portability classifications assigned
- [x] Flattening risks documented
- [x] Simulated behavior documented
- [x] Target compatibility matrix created
- [x] No naming collisions found
- [x] All commands parse correctly
- [x] Rulesync.jsonc compatibility verified

// turbo
