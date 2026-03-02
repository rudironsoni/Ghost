---
description: 'Initialize dotnet-agent-harness context for this repository'
targets: ['*']
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Initialize .NET project context and load routing'
antigravity:
  trigger: '/init-project'
---

Initialize dotnet-agent-harness context for this repository.

1. Invoke [skill:dotnet-advisor].
2. Invoke [skill:dotnet-version-detection].
3. Invoke [skill:dotnet-project-analysis].
4. If this is a single-file .NET app without a .csproj, invoke [skill:dotnet-file-based-apps].
5. If Serena MCP tools are available, run onboarding flow:
   - `serena_activate_project` with `project: "."`
   - `serena_check_onboarding_performed`
   - If onboarding is false, call `serena_onboarding`
6. Summarize detected project context:
   - SDK and TFM
   - solution/project count
   - dominant app type (API, library, CLI, UI)
   - recommended specialist skills

Do not modify files in this command; this is analysis-only initialization.
