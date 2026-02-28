---
description: Package wiki as VitePress dark-theme site
targets: ['*']
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
