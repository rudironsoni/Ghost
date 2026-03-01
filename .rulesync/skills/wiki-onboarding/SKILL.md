---
name: wiki-onboarding
description: Generates audience-specific onboarding guides
license: MIT
targets: ['claudecode', 'codexcli']
tags: ['wiki', 'onboarding', 'documentation']
version: '1.0.0'
author: 'microsoft'
invocable: true
---

# wiki-onboarding

Generates audience-specific onboarding guides

## Trigger

- User asks for onboarding docs
- User wants getting-started guides

## Guides Generated

1. **Contributor Guide**: Setup, workflow, PR process
2. **Staff Engineer Guide**: Architecture, patterns, key decisions
3. **Executive Guide**: Business value, roadmap, metrics
4. **PM Guide**: Features, user flows, release planning

## Output

````text
wiki/onboarding/
├── contributor.md
├── staff-engineer.md
├── executive.md
└── pm.md
```text

## Design Principles

- Audience-specific content
- Progressive disclosure
- Source-linked citations
- Actionable next steps
````
