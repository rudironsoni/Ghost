---
description: Generate GitHub Actions workflow to deploy wiki
targets: ['*']
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
