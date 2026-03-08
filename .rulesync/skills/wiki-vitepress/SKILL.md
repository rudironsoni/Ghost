---
name: wiki-vitepress
category: developer-experience
subcategory: cli
description: Packages wiki as VitePress dark-theme site
license: MIT
targets: ['claudecode', 'codexcli']
tags: [wiki, vitepress, documentation]
version: '1.0.0'
author: 'microsoft'
invocable: true
claudecode: {}
opencode: {}
codexcli:
  short-description: 'Toolkit guidance for wiki-vitepress'
copilot: {}
geminicli: {}
antigravity: {}
---

# wiki-vitepress

Packages wiki as VitePress dark-theme site

## Trigger

- User asks to build a site
- User wants to package wiki as VitePress

## Features

- Dark theme by default
- Click-to-zoom Mermaid diagrams
- Search functionality
- Responsive design
- Sidebar navigation

## Prerequisites

Run `/deep-wiki:generate` first

## Output

````text
wiki/.vitepress/dist/
```text

## Local Preview

```bash
cd wiki
npx vitepress dev
```text

## Deployment

Use `/deep-wiki:deploy` to generate GitHub Actions workflow.
````
