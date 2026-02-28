---
name: wiki-page-writer
description: Generates pages with dark-mode Mermaid diagrams
license: MIT
targets: ['claudecode', 'codexcli']
tags: ['wiki', 'documentation', 'mermaid']
version: '1.0.0'
author: 'microsoft'
invocable: true
---

# wiki-page-writer

Generates pages with dark-mode Mermaid diagrams

## Trigger

- User asks to document a component
- User wants technical deep-dive

## Capabilities

- Generates rich Markdown pages
- Creates dark-mode Mermaid diagrams
- Source-linked citations
- Architecture documentation

## Requirements

- Minimum 3-5 Mermaid diagrams per page
- All citations use [file:line](URL) format
- Dark-mode native output
- Table-driven over prose

## Diagram Types

- Flowcharts
- Sequence diagrams
- ER diagrams
- State diagrams
- Class diagrams

## Output

Single markdown file in `wiki/pages/<topic-slug>.md`
