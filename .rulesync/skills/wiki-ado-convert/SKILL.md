---
name: wiki-ado-convert
description: Converts VitePress wiki to Azure DevOps Wiki-compatible format
license: MIT
targets: ['claudecode', 'codexcli']
tags: ['wiki', 'azure-devops', 'conversion']
version: '1.0.0'
author: 'microsoft'
invocable: true
---

# wiki-ado-convert

Converts VitePress wiki to Azure DevOps Wiki-compatible format

## Trigger

- User asks to export wiki for Azure DevOps
- User wants to convert Mermaid/markdown for ADO

## Capabilities

- Converts Mermaid diagrams to ADO-compatible format
- Adjusts markdown for ADO Wiki
- Handles link transformations

## Output

`scripts/convert-to-ado.js` - Node.js conversion script

## Usage

````bash
node scripts/convert-to-ado.js
```text

Creates `wiki-ado/` folder with converted content.
````
