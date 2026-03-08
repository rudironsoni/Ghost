---
name: deep-wiki-research
description: '$1'
targets: ['*']
portability: claude-opencode
flattening-risk: high
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'WebSearch']
copilot:
  description: 'Deep research with citations'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:research

Multi-turn deep investigation with evidence-based analysis

## Usage

````bash
/deep-wiki:research <topic>
```text

## Parameters

- `topic`: The subject to research in-depth

## Description

Performs deep, iterative research across the codebase:

1. Initial broad scan
2. Targeted deep dives (5 iterations)
3. Evidence collection with citations
4. Synthesis into comprehensive report

## Standards

- Zero tolerance for shallow analysis
- Every claim cites `file_path:line_number`
- Cross-references multiple sources
- Identifies edge cases and trade-offs

## Examples

```bash
/deep-wiki:research How does the caching layer work?
/deep-wiki:research Authentication flow
/deep-wiki:research Database transaction handling
```text

## Output

Research report in `wiki/research/<topic-slug>.md`
````
