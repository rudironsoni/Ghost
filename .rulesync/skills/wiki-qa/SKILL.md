---
name: wiki-qa
description: Answers questions about the repository
license: MIT
targets: ['claudecode', 'codexcli']
tags: ['wiki', 'qa', 'documentation']
version: '1.0.0'
author: 'microsoft'
invocable: true
---

# wiki-qa

Answers questions about the repository

## Trigger

- User asks a question about the repo
- User wants to understand how something works

## Capabilities

- Answers questions grounded in source code
- Provides source-linked citations
- Explains architecture and implementation

## Standards

- Every answer cites file_path:line_number
- References actual implementations
- No assumptions or guesses

## Examples

- What database migrations exist?
- How is dependency injection configured?
- Where is user authentication handled?
