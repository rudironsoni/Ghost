---
description: Generate structured changelog from git commits
trigger: /deep-wiki-changelog
turbo: true
---
# Workflow: /deep-wiki-changelog

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

// turbo
