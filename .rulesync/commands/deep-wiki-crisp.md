---
description: Fast wiki generation (concise, rate-limit-friendly)
targets: ['*']
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
