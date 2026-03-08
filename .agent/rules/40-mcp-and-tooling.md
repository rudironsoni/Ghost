---
trigger: always_on
---
# MCP Server Policies and Tooling Strategy

This document defines the MCP server inventory, intended use cases, routing preferences, and health check expectations for the dotnet-agent-harness ecosystem.

## MCP Server Inventory

The following MCP servers are configured in `.rulesync/mcp.json`:

| Server | Type | Primary Role | Best Use Cases |
|--------|------|--------------|----------------|
| **serena** | stdio | Semantic code analysis | Symbol-level navigation, refactoring, dependency analysis, precise code edits |
| **context7** | http | Documentation intelligence | Up-to-date library/framework documentation queries, API discovery |
| **microsoftdocs-mcp** | http | Official Microsoft documentation | First-party .NET/ASP.NET/Azure docs, authoritative API references |
| **deepwiki** | http | Repository documentation | Project-specific docs, wiki content, generated documentation |
| **github** | http | Repository operations | PRs, issues, Actions, repository metadata |

## MCP Routing Guide

### Primary Routing Decision Tree

```text
Need to navigate or refactor code?
  YES -> [mcp:serena] for symbol operations
  NO -> Continue...

Need official Microsoft documentation?
  YES -> [mcp:microsoftdocs-mcp] for .NET/Azure docs
  NO -> Continue...

Need third-party library documentation?
  YES -> [mcp:context7] for up-to-date API docs
  NO -> Continue...

Need project-specific documentation?
  YES -> [mcp:deepwiki] for repo wiki/docs
  NO -> Continue...

Need GitHub operations?
  YES -> [mcp:github] for repos, PRs, issues
  NO -> Traditional tools (Read, Grep, Glob, Bash)
```

### Server-Specific Guidance

#### Serena (Code Navigation & Refactoring)

**Preferred for:**
- Finding symbol definitions across the codebase
- Understanding file structure and organization
- Tracking symbol references for impact analysis
- Precise code edits with automatic reference updates
- Renaming symbols with full codebase propagation

**Tool mapping:**
- `serena_find_symbol` → Find class/method definitions
- `serena_get_symbols_overview` → Understand file structure
- `serena_find_referencing_symbols` → Track dependencies
- `serena_replace_symbol_body` → Precise modifications
- `serena_rename_symbol` → Safe refactoring

**When to prefer over traditional tools:**
- ✅ Navigation tasks (vs Grep for finding symbols)
- ✅ Refactoring operations (vs Edit for symbol bodies)
- ✅ Dependency analysis (vs manual code inspection)
- ✅ Multi-file changes requiring reference updates

**Fallback:** If Serena unavailable, use Read + Grep + Edit.

#### MicrosoftDocs MCP

**Preferred for:**
- Official .NET API documentation
- ASP.NET Core guidance
- Azure SDK documentation
- Breaking changes and migration guides
- Security advisories and best practices

**Tool mapping:**
- `microsoftdocs-mcp_microsoft_docs_search` → Search official docs
- `microsoftdocs-mcp_microsoft_code_sample_search` → Find code samples
- `microsoftdocs-mcp_microsoft_docs_fetch` → Read full articles

**When to use:**
- ✅ Before implementing features using Microsoft APIs
- ✅ When unsure about API availability in specific TFMs
- ✅ For authoritative answers on .NET behavior
- ✅ Security and compliance guidance

**Fallback:** Use web search with official microsoft.com/learn sources.

#### Context7

**Preferred for:**
- Third-party library documentation (NuGet packages)
- Framework-specific patterns and examples
- Recent documentation updates not in offline caches
- Community-contributed documentation

**When to use:**
- ✅ Questions about specific library versions
- ✅ Comparing library alternatives
- ✅ Finding usage examples for OSS packages

**Fallback:** Use web search with github.com or official library docs.

#### DeepWiki

**Preferred for:**
- Repository-specific documentation
- Generated API docs for this project
- Architecture decision records (ADRs)
- Contribution guidelines and workflows

**When to use:**
- ✅ Understanding project conventions
- ✅ Finding internal documentation
- ✅ Navigating generated docs

**Fallback:** Read files from docs/ or wiki/ directories directly.

#### GitHub MCP

**Preferred for:**
- Repository operations
- Pull request management
- Issue tracking
- Actions workflow inspection
- Release management

**Tool mapping:**
- `github` namespace commands
- PR creation, review, and merge operations

**When to use:**
- ✅ Creating or reviewing PRs
- ✅ Querying issue status
- ✅ Repository metadata inspection

**Fallback:** Use gh CLI or web interface.

## Target-Specific Tool Filtering

### OpenCode Behavior

- Tab cycles **primary** agents only
- `@mention` invokes subagents
- Tool availability varies by agent configuration
- Some MCP servers may be filtered based on target surface

### Codex CLI Behavior

- Full MCP server access
- All configured servers available
- No tool filtering applied

### Copilot Behavior

- Uses `execute` tool for shell commands
- MCP access via extension points
- May require explicit `@github` mentions

### Claude Code Behavior

- Native MCP server support
- All configured servers available via tool calls
- Automatic routing based on tool names

### Gemini CLI Behavior

- MCP support via configuration
- Similar to Claude Code for stdio/http servers

## Health Check Expectations

### Session Start Validation

Every session should validate MCP server health:

1. **Check configuration:** Ensure `.rulesync/mcp.json` exists and is valid
2. **Test connectivity:** 
   - HTTP servers: Verify URL accessibility
   - STDIO servers: Verify command availability (uvx, npx, etc.)
3. **Report status:** Output which servers are available/unavailable
4. **Suggest alternatives:** Provide fallback guidance for unavailable servers

### Health Check Output Format

```json
{
    "mcpHealth": {
    "serena": { "status": "available", "type": "stdio" },
    "microsoftdocs-mcp": { "status": "available", "type": "http" },
    "context7": { "status": "unavailable", "type": "http", "reason": "network_timeout" },
    "deepwiki": { "status": "available", "type": "http" },
    "github": { "status": "available", "type": "http" }
  },
  "recommendations": [
    "context7 unavailable: Use web search for third-party docs"
  ]
}
```

### Failure Handling

| Server Down | Impact | Mitigation |
|-------------|--------|------------|
| serena | Cannot perform symbol navigation | Use Read + Grep for finding code |
| microsoftdocs-mcp | No official doc queries | Use web search with microsoft.com/learn |
| context7 | No third-party docs | Use web search or library READMEs |
| deepwiki | No repo docs | Read markdown files directly |
| github | No repo operations | Use gh CLI or web interface |

## MCP Usage Patterns by Agent

### dotnet-architect

**Preferred MCPs:**
1. **serena** - Analyze existing solution structure
2. **microsoftdocs-mcp** - Validate framework choices against official guidance
3. **deepwiki** - Check project conventions

**Example workflow:**
```
1. [mcp:serena] Analyze solution structure
2. [mcp:microsoftdocs-mcp] Query official architecture guidance
3. Recommend approach based on findings
```

### dotnet-code-review-agent

**Preferred MCPs:**
1. **serena** - Navigate code under review
2. **microsoftdocs-mcp** - Verify API usage against docs
3. **context7** - Check third-party library patterns

**Example workflow:**
```
1. [mcp:serena] Find symbols in changed files
2. Review code with symbol context
3. [mcp:microsoftdocs-mcp] Validate API usage
```

### dotnet-advisor (Router)

**Preferred MCPs:**
1. **serena** - Quick project analysis
2. **microsoftdocs-mcp** - Skill documentation validation

**Example workflow:**
```
1. [mcp:serena] Get project overview
2. Route to appropriate skill
3. [mcp:microsoftdocs-mcp] Validate skill guidance
```

## Implementation Notes

### Adding New MCP Servers

When adding new MCP servers:

1. Add entry to `.rulesync/mcp.json`
2. Document role and use cases in this file
3. Update routing guidance in relevant subagents
4. Add health check logic to session-start hook
5. Update target-specific configurations if needed

### MCP Server Categories

| Category | Servers | Use When |
|----------|---------|----------|
| **Code Intelligence** | serena | Navigation, refactoring, analysis |
| **Documentation** | microsoftdocs-mcp, context7, deepwiki | Research, validation, examples |
| **Operations** | github | Repository management |

### Performance Considerations

- **HTTP MCPs:** May have network latency; cache results when appropriate
- **STDIO MCPs:** Startup cost on first use; keep sessions alive
- **Parallel calls:** Independent MCP calls can be parallelized
- **Fallbacks:** Always have traditional tool fallback for offline scenarios

## References

- MCP Configuration: `.rulesync/mcp.json`
- Health Check Hook: `.rulesync/hooks/session-start.sh`
- Serena Documentation: https://github.com/oraios/serena
- MCP Specification: https://modelcontextprotocol.io/
