---
name: deep-wiki-crisp
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
  description: 'Generate crisp one-page summary'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:crisp

Fast wiki generation — concise, parallelized, rate-limit-friendly

## Usage

````bash
/deep-wiki:crisp
```text

## Description

Generates a streamlined wiki optimized for speed and API rate limits:

- 5–8 core pages only
- Parallel generation
- No VitePress build step
- Quick structure overview

## Output

```text
wiki-crisp/
├── catalogue.json
├── pages/
│   ├── index.md
│   ├── architecture.md
│   ├── getting-started.md
│   └── api-reference.md
└── README.md
```text

## When to Use

- Quick documentation needs
- Rate limit concerns
- Rapid prototyping
- Smaller codebases
````
