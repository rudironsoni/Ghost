---
description: Generate complete wiki with pages and onboarding
targets: ['*']
---

# /deep-wiki:generate

Generate a complete wiki — catalogue + all pages + onboarding guides + VitePress site

## Usage

````bash
/deep-wiki:generate
```text

## Description

Generates a comprehensive wiki documentation site from the current codebase:

1. **Catalogue**: Creates hierarchical JSON table of contents
2. **Pages**: Generates rich Markdown pages with dark-mode Mermaid diagrams
3. **Onboarding**: Creates 4 audience-specific onboarding guides
4. **VitePress**: Packages everything into a dark-theme static site

## Output Structure

```text
wiki/
├── catalogue.json          # Hierarchical TOC
├── pages/                  # Generated documentation pages
│   ├── index.md
│   ├── architecture/
│   ├── components/
│   └── ...
├── onboarding/             # Audience-specific guides
│   ├── contributor.md
│   ├── staff-engineer.md
│   ├── executive.md
│   └── pm.md
└── .vitepress/            # VitePress configuration
    ├── config.ts
    └── theme/
```text

## Examples

```bash
# Generate full wiki
/deep-wiki:generate

# After generation, build the site
/deep-wiki:build
```text
````
