---
name: deep-wiki-build
description: '$1'
targets: ['*']
portability: copilot-gemini
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Glob', 'Bash']
copilot:
  description: 'Build VitePress wiki site'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:build

Package generated wiki as a VitePress site with dark theme

## Usage

````bash
/deep-wiki:build
```text

## Description

Packages the generated wiki into a VitePress static site with:

- Dark theme by default
- Click-to-zoom Mermaid diagrams
- Search functionality
- Responsive design
- Sidebar navigation

## Prerequisites

Run `/deep-wiki:generate` first

## Output

```text
wiki/.vitepress/dist/      # Static site output
```text

## Local Preview

```bash
cd wiki
npx vitepress dev
```text
````
