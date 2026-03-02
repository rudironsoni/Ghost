---
name: microsoft-learn-mcp
description: Microsoft Learn MCP Server integration for accessing official Microsoft documentation
license: MIT
targets: ['*']
tags: ['microsoft', 'learn', 'mcp', 'documentation', 'skill']
version: '0.0.1'
author: 'dotnet-agent-harness'
invocable: true
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
---

# Microsoft Learn MCP

Access trusted and up-to-date Microsoft Learn documentation via the Model Context Protocol (MCP).

## Overview

The Microsoft Learn MCP Server enables AI agents and development environments to bring official Microsoft documentation
directly into their workflows. It provides a remote MCP server endpoint that supports searching documentation, fetching
complete articles, and searching through code samples.

## Endpoint

```text
https://learn.microsoft.com/api/mcp
```

> **Note:** This endpoint is designed for programmatic access by MCP clients via Streamable HTTP. It does not support
> direct access from a web browser.

## Use Cases

- Enhance agentic IDEs like VS Code and Visual Studio with Learn content
- Leverage Learn content in Copilot Studio agents, Foundry agents, and custom solutions
- Enable engineers to use official Microsoft documentation in their flow of work
- Access up-to-date API references, conceptual documentation, and code samples

## Capabilities

### Search Documentation

Search through Microsoft's official documentation using natural language queries:

- API references
- Conceptual documentation
- Quickstarts and tutorials
- Code samples and examples

### Fetch Complete Articles

Retrieve full documentation articles with:

- Complete content and structure
- Code samples and examples
- Related links and references
- Version-specific information

### Code Sample Search

Find relevant code samples across Microsoft documentation:

- C# examples
- Configuration samples
- Best practice implementations
- Platform-specific code

## Supported Clients

The Microsoft Learn MCP Server works with:

- **GitHub Copilot** (VS Code, Visual Studio)
- **Microsoft Copilot Studio**
- **Azure AI Foundry**
- **Custom MCP clients**
- **Any MCP-compatible agentic development environment**

## Getting Started

### VS Code

1. Install the MCP extension or use GitHub Copilot
2. Configure the Microsoft Learn MCP Server endpoint
3. Start querying Microsoft Learn content

See: [Get Started in VS Code](https://learn.microsoft.com/en-us/training/support/mcp-get-started)

### Azure AI Foundry

1. Access your Foundry project
2. Add the Microsoft Learn MCP Server as a data source
3. Use in your agent workflows

See: [Get Started in Foundry](https://learn.microsoft.com/en-us/training/support/mcp-get-started-foundry)

## Requirements

- No authentication required
- Usage covered by [Microsoft Learn Terms of Use](https://learn.microsoft.com/en-us/legal/termsofuse)
- [Microsoft API Terms of Use](https://learn.microsoft.com/en-us/legal/microsoft-apis/terms-of-use) apply
- Publicly available at no charge

## Limitations

- Contains publicly available documentation only
- Does not include training modules, learning paths, instructor-led courses, or exams (use
  [Learn Catalog API](https://learn.microsoft.com/en-us/training/support/catalog-api) for those)
- Knowledge service refreshes incrementally after content updates and completely once daily
- Updates communicated via [Release Notes](https://learn.microsoft.com/en-us/training/support/mcp-release-notes)

## Best Practices

1. **Use for Documentation References**: Query for API documentation, conceptual guides, and samples
2. **Combine with Other Tools**: Use alongside code search tools for comprehensive context
3. **Validate Critical Information**: For production-critical decisions, verify against latest official docs
4. **Cache Strategically**: Consider caching responses for frequently accessed documentation

## Related Resources

- [Microsoft Learn MCP Server Developer Documentation](https://learn.microsoft.com/en-us/training/support/mcp-developer-reference)
- [Release Notes](https://learn.microsoft.com/en-us/training/support/mcp-release-notes)
- [Best Practices](https://learn.microsoft.com/en-us/training/support/mcp-best-practices)
- [Microsoft Learn](https://learn.microsoft.com/)
