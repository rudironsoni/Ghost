---
name: deep-wiki-catalogue
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
  description: 'Generate structured repository catalogue'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:catalogue

Generate only the hierarchical wiki structure as JSON

## Usage

````bash
/deep-wiki:catalogue
```text

## Description

Creates a structured table of contents without generating actual content pages. Useful for:

- Reviewing wiki structure before full generation
- Custom page generation pipelines
- Integration with other tools

## Output Format

```json
{
  "title": "Project Wiki",
  "sections": [
    {
      "title": "Architecture",
      "path": "architecture",
      "subsections": [...]
    }
  ]
}
```text
````
