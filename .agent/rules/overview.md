---
trigger: always_on
---
# dotnet-agent-harness

Comprehensive .NET development guidance for modern C#, ASP.NET Core, MAUI, Blazor, and cloud-native apps.

## RuleSync-first architecture

- `.rulesync/` is the source of truth for this repository's agent content. Author changes in `rules`, `skills`,
  `subagents`, `commands`, `hooks`, and `mcp` there first.
- Generated target directories and prompt files are build artifacts. Regenerate them from RuleSync instead of editing
  them by hand.
- Keep the shared root `AGENTS.md` content in this overview rule and keep follow-up split rules off AGENTS-writing
  targets so regeneration stays stable.
- Runtime commands, exported bundles, and published agent packs all inherit from the RuleSync-authored catalog. When
  generated output looks wrong, fix `.rulesync/` and re-run `rulesync`.

## Surface ownership

- `rules`: always-on repository guidance, routing, and cross-cutting constraints.
- `skills`: reusable domain knowledge and implementation standards.
- `subagents`: named specialists with bounded scope and tool budgets.
- `commands`: user-invocable entry points that should prefer the local runtime over hand-written procedures.
- `hooks`: ambient reminders and lightweight automation; keep them advisory and portable.
- `mcp`: shared MCP server registrations and transport metadata for targets that support MCP.

## Working contract

- Keep frontmatter strictly RuleSync-compliant and add target-specific blocks only for real runtime differences.
- Keep commands deterministic, keep hooks advisory, and route hook-only targets through generated rules or hook text
  instead of unavailable skill or command surfaces.
- Validate and regenerate with the system-wide `rulesync` binary: use `rulesync generate --check` as the consistency
  gate, then `rulesync generate` when changes are intentional.
- Review source and generated diffs together; do not hand-edit generated target directories to patch a platform quirk.
- **Verify target configurations** using the per-target checklists in `.rulesync/verification/` before committing changes.

## Platform model

This toolkit provides:

- 193 skills
- 18 specialist agents/subagents
- shared RuleSync rules, commands, hooks, and MCP config

Compatible targets include:

- Claude Code
- GitHub Copilot CLI
- OpenCode
- Codex CLI
- Gemini CLI
- Antigravity
- Factory Droid

Target support is intentionally asymmetric. Author the shared behavior once, then add target-specific blocks only where
the runtime surface actually differs. See `.rulesync/rules/15-target-surfaces.md` for the target matrix and authoring
rules.

## Recommended install

Use RuleSync to install this toolkit in your repository:

```bash
rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

Declarative installation:

```bash
rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

If you use declarative sources:

```jsonc
{
  "sources": [{ "source": "rudironsoni/dotnet-agent-harness", "path": ".rulesync" }],
}
```

```bash
rulesync install
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

## OpenCode behavior

- Tab cycles **primary** agents only.
- `@mention` invokes subagents.
- `dotnet-architect` is configured as a primary OpenCode agent in this toolkit so it can appear in Tab rotation.

## Operating modes

- Architect: lead with the recommended repository-specific structure, trade-offs, and next implementation steps.
- Implementer: make the smallest safe change, verify it with narrow repo-native commands, and report target plus
  verification.
- Reviewer: produce findings-first output with severity, evidence, and risk.
- Tester: define the narrowest effective verification path, including exact commands and residual risk.

## Hook coverage

- Gemini CLI inherits the shared .NET session routing and now also receives the thin post-edit Roslyn, formatting, and
  Slopwatch advisories generated from `.rulesync/hooks.json`.
- Factory Droid consumes the same .NET session and MCP routing through generated rules and hooks, but its hook reminders
  stay compatible with that runtime's rules-plus-hooks surface instead of requiring imported skills or commands.

## Verification

Before committing changes or releasing updates, verify target configurations using the per-target checklists:

### Quick Verification
```bash
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*" --check
```

### Per-Target Checklists
- [Claude Code](verification/claudecode-checklist.md) - Full feature verification
- [OpenCode](verification/opencode-checklist.md) - Tab and @mention verification
- [GitHub Copilot CLI](verification/copilot-checklist.md) - Tool name mapping verification
- [Gemini CLI](verification/geminicli-checklist.md) - Portable hooks verification
- [Codex CLI](verification/codexcli-checklist.md) - Read-only sandbox verification
- [Factory Droid](verification/factorydroid-checklist.md) - Rules-only delivery verification
- [Antigravity](verification/antigravity-checklist.md) - Concise, portable verification

See [Verification README](verification/README.md) for detailed verification strategy.

## Troubleshooting

If RuleSync reports `Multiple root rulesync rules found`, ensure only one root overview rule exists in
`.rulesync/rules/`.

Use RuleSync commands directly instead of manually reproducing catalog, prompt, incident, or graph logic from source
files.

## Contributing

Edit source files in `.rulesync/` and validate with `npm run ci:rulesync`.

## License

MIT License. See `LICENSE`.
