---
name: deep-wiki-onboard
description: '$1'
targets: ['*']
portability: copilot-gemini
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Generate onboarding guides'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:onboard

Generate 4 audience-tailored onboarding guides

## Usage

````bash
/deep-wiki:onboard
```text

## Description

Creates comprehensive onboarding documentation for different audiences:

1. **Contributor Guide**: Setup, workflow, PR process
2. **Staff Engineer Guide**: Architecture, patterns, key decisions
3. **Executive Guide**: Business value, roadmap, metrics
4. **PM Guide**: Features, user flows, release planning

## Output

```text
wiki/onboarding/
├── contributor.md
├── staff-engineer.md
├── executive.md
└── pm.md
```text
````
