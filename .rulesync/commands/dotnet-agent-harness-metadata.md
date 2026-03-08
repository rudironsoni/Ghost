---
name: dotnet-agent-harness-metadata
description: '$1'
targets: ['*']
portability: copilot-gemini
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Bash']
copilot:
  description: 'Show installation metadata'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:metadata

Use the local runtime to inspect NuGet usage, namespaces, and types without hand-parsing project files or assemblies.

## Execution Contract

```bash
dotnet agent-harness metadata packages --target src/MyApp/MyApp.csproj --format json
dotnet agent-harness metadata types --target src/MyLib/MyLib.csproj --namespace MyLib.Core --build
dotnet agent-harness metadata type --target src/MyLib/MyLib.csproj --type MyLib.Core.OrderService --build
```

## What It Does

1. `packages` parses direct package references and central package management data from project files
2. `namespaces`, `types`, and `type` inspect a compiled assembly from a `.dll` or a project output
3. optional `--build` compiles the target project before metadata inspection when no assembly exists yet
4. returns structured results for agent reasoning or JSON automation

## Options

- `packages|namespaces|types|type`: metadata mode
- `--target <path>`: `.csproj`, `.dll`, solution, or directory depending on mode
- `--assembly <path>`: inspect a specific compiled assembly directly
- `--query <text>`: filter packages, namespaces, or types
- `--namespace <value>`: constrain `types` to a namespace
- `--type <Fully.Qualified.Name>`: exact or simple type name for `type` mode
- `--configuration <Debug|Release>`: build/output configuration. Defaults to `Debug`
- `--framework <tfm>`: choose a target framework for multi-target projects
- `--build`: run `dotnet build` before assembly inspection
- `--limit <N>`: cap returned package, namespace, or type rows

## Notes

- Prefer fully qualified type names for deterministic inspection.
- `packages` is local and deterministic; it does not query NuGet.org for newer versions.
- Use this instead of ad hoc `grep` when an agent needs authoritative package or compiled-type shape.
