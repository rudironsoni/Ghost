---
name: deep-wiki-changelog
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
  description: 'Generate changelog from git commits'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:changelog

Generate a structured changelog from git commits

## Usage

````bash
/deep-wiki:changelog
```text

## Description

Analyzes git history and generates a categorized changelog:

- Features
- Bug Fixes
- Documentation
- Breaking Changes
- Deprecations

## Output

```text
wiki/
└── changelog.md
```text
````
