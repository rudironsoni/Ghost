---
name: deep-wiki-ado
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
  description: 'Convert wiki to Azure DevOps Wiki format'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:ado

Generate a Node.js script to convert wiki to Azure DevOps Wiki-compatible format

## Usage

````bash
/deep-wiki:ado
```text

## Description

Creates a conversion script that:

- Converts Mermaid diagrams to ADO-compatible format
- Adjusts markdown for ADO Wiki
- Handles link transformations

## Output

```text
scripts/
└── convert-to-ado.js
```text

## Usage After Generation

```bash
node scripts/convert-to-ado.js
```text

The script will create `wiki-ado/` folder with converted content.
````
