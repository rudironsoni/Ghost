---
name: dotnet-agent-harness-bootstrap
description:
  'Bootstrap dotnet-agent-harness as a local .NET tool, write RuleSync config, and initialize target-specific agent
  outputs'
targets: ['*']
portability: universal
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:bootstrap

Bootstrap the runtime harness for OpenCode, Claude Code, Codex CLI, Gemini CLI, GitHub Copilot CLI, Antigravity, and
Factory Droid.

## Execution Contract

```bash
dotnet agent-harness bootstrap --profile platform-native --targets claudecode,opencode,codexcli,geminicli,copilot,antigravity,factorydroid --enable-pack dotnet-intelligence --run-rulesync
```

## What It Does

1. writes or updates `.config/dotnet-tools.json` for the local `dotnet-agent-harness` tool unless persistence is
   disabled
2. writes `rulesync.jsonc` with the selected targets and toolkit source
3. writes a target-aware RuleSync feature map so unsupported feature/target combinations are not requested
4. optionally installs bootstrap packs such as `dotnet-intelligence` into the local tool manifest and repo config unless
   persistence is disabled
5. writes repo-local state under `.dotnet-agent-harness/` unless persistence is disabled
6. optionally runs `rulesync install` and `rulesync generate`
7. reports which platform output roots will be generated

## Options

- `--profile <core|platform-native|full>`: bootstrap preset for RuleSync features. Defaults to `platform-native`
- `--targets <csv>`: platform list. Supported ids: `claudecode`, `opencode`, `codexcli`, `geminicli`, `copilot`,
  `antigravity`, `factorydroid`
- `--features <csv>`: explicit RuleSync features to generate. Overrides the selected profile defaults
- `--enable-pack <csv>`: optional packs to install. Current pack: `dotnet-intelligence`
- `--source <owner/repo>`: RuleSync source repository. Defaults to `rudironsoni/dotnet-agent-harness`
- `--source-path <path>`: install path for the RuleSync source. Defaults to `.rulesync`
- `--tool-version <x.y.z>`: pin a specific local tool version in `.config/dotnet-tools.json`
- `--run-rulesync`: run `rulesync install` and `rulesync generate` after writing config
- `--force`: overwrite an existing `rulesync.jsonc`
- `--no-save`: skip implicit repo-local persistence such as `.config/dotnet-tools.json`, pack files, and
  `.dotnet-agent-harness/` state while still writing the selected RuleSync config

## Notes

- Use this in consumer repositories where you want the local runtime and generated platform files to stay in sync.
- `dotnet-intelligence` currently installs Slopwatch, writes `.slopwatch/config.json`, and enables advisory post-edit
  hooks once generated files are present.
- The generated `rulesync.jsonc` uses per-target `features` entries so mixed target sets do not request unsupported
  surfaces.
- `--no-save` is intended for dry-ish bootstrap previews and smoke tests where you want the RuleSync contract file
  without mutating repo-local tool or state files.
- If RuleSync is not installed, bootstrap still writes the repo contract and tells you what to run next.
- Prefer this over ad hoc per-platform setup when the repository is meant to support multiple agent runtimes.
