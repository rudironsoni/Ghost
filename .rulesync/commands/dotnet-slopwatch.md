---
description: 'Run Slopwatch quality gate for .NET projects'
targets: ['*']
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Run Slopwatch analyze and report findings'
antigravity:
  trigger: '/dotnet-slopwatch'
---

Run Slopwatch as an explicit, invocable command.

1. First, load and follow [skill:dotnet-slopwatch] as the execution guide for this command. Do not invent alternate
   slopwatch flows; use the skill's installation, strict-mode analysis, baseline, and remediation rules.

2. Verify tool is available:
   - Prefer local manifest: `dotnet tool restore`
   - If unavailable, install globally: `dotnet tool install --global Slopwatch.Cmd`

3. Run analysis in strict mode:
   - `slopwatch analyze -d . --fail-on warning`

4. If findings exist:
   - Summarize by rule (SW001-SW006)
   - Include file and line for each finding
   - Recommend concrete remediation (do not suppress)

5. If no findings:
   - Report pass and remind to keep baseline intentional

Never auto-update baseline unless the user explicitly requests it.
