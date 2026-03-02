---
name: github
description:
  'GitHub MCP Server integration for repository operations, pull requests, issues, and GitHub Actions. Triggers on:
  github mcp, github operations, pull request, issue, repository, github actions, commit, branch, merge.'
license: MIT
targets: ['*']
tags: ['github', 'mcp', 'repository', 'skill', 'vcs/git']
version: '0.0.1'
author: 'dotnet-agent-harness'
invocable: true
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
---

# GitHub MCP

Access GitHub repositories, pull requests, issues, and GitHub Actions via the Model Context Protocol (MCP).

## Overview

The GitHub MCP Server enables AI agents to interact with GitHub programmatically. It provides tools for repository
operations, code review, issue management, and CI/CD pipeline inspection.

## Configuration

The MCP server is configured in `.rulesync/mcp.json`:

```json
{
  "mcpServers": {
    "github": {
      "type": "remote",
      "url": "https://api.githubcopilot.com/mcp/",
      "oauth": false,
      "headers": {
        "Authorization": "Bearer {env:GITHUB_TOKEN}"
      },
      "enabled": true
    }
  }
}
```

> **Prerequisites:** Set `GITHUB_TOKEN` environment variable with a GitHub Personal Access Token.

## Capabilities

### Repository Operations

- List repositories for authenticated user or organization
- Create new repositories
- Get repository details and metadata
- Clone and fork repositories
- Manage repository settings

### Pull Requests

- List open/closed PRs
- Create new pull requests
- Get PR details and diff
- Add comments and reviews
- Merge pull requests
- Update PR status and labels

### Issues

- List issues with filters
- Create new issues
- Update issue status and labels
- Add comments
- Manage milestones and projects

### Code Review

- Get file contents and diffs
- Review PR changes
- Request changes or approve
- Check review status

### GitHub Actions

- List workflows
- Get workflow runs
- Trigger workflows
- Check workflow status and logs

### Commits and Branches

- List commits with filters
- Get commit details
- Compare branches
- Create/delete branches
- Protect branches

## Use Cases

- Automate PR reviews and issue triage
- Monitor CI/CD pipeline status
- Generate release notes from PRs
- Analyze repository metrics
- Manage project boards programmatically

## Best Practices

- Use fine-grained PATs with minimal required scopes
- Handle rate limiting gracefully
- Cache frequently accessed data
- Validate permissions before operations
- Log actions for audit trails

## References

- [GitHub MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/github)
- [GitHub API Documentation](https://docs.github.com/en/rest)
- [Creating Personal Access Tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
