---
name: deep-wiki-deploy
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
  description: 'Deploy wiki to GitHub Pages'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:deploy

Generate GitHub Actions workflow to deploy wiki to GitHub Pages

## Usage

````bash
/deep-wiki:deploy
```text

## Description

Creates a GitHub Actions workflow that:

1. Builds the VitePress site
2. Deploys to GitHub Pages
3. Runs on push to main

## Output

```text
.github/workflows/
└── deploy-wiki.yml
```text

## Manual Trigger

After generation, commit and push the workflow:

```bash
git add .github/workflows/deploy-wiki.yml
git commit -m "Add wiki deployment workflow"
git push origin main
```text
````
