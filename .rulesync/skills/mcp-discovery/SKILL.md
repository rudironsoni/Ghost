---
name: mcp-discovery
description: >-
  Discovers available MCP servers from registries and validates compatibility. Auto-discovers MCP servers from official
  registries, community sources, and local definitions. Triggers on: mcp discovery, find mcp servers, discover mcp, mcp
  registry, mcp catalog.
targets: ['*']
tags: ['dotnet', 'skill', 'mcp', 'discovery']
version: '0.0.1'
author: 'dotnet-agent-harness'
license: MIT
claudecode:
  model: inherit
  allowed-tools:
    - Read
    - Grep
    - Glob
    - Bash
    - WebFetch
opencode:
  mode: 'primary'
  tools:
    bash: true
    edit: false
    write: false
copilot:
  tools: ['read', 'search', 'execute']
---

# MCP Discovery

Discovers and catalogs available MCP servers from registries, community sources, and local configurations.

## Preloaded Skills

Always load before analysis:

- [skill:dotnet-advisor] -- routing and context
- [skill:mcp-health] -- validate discovered MCPs

## Usage

````bash
# Discover all MCP servers
[mcp-discovery:find-all]

# Search MCP registry
[mcp-discovery:search <keyword>]

# Update local MCP config
[mcp-discovery:update-config]

# Show available MCPs
[mcp-discovery:list]
```text
## MCP Registry Sources

### Official Registries

1. **MCP Registry (modelcontextprotocol.io)**
   - Official MCP server listings
   - Verified implementations
   - Community submissions

2. **Smithery Registry (smithery.ai)**
   - Curated MCP servers
   - Ratings and reviews
   - Installation guides

### Discovery Methods

#### From Official Registry

```bash
# Fetch MCP registry
https://registry.modelcontextprotocol.io/servers

# Parse and filter for .NET compatible
jq '.servers[] | select(.tags[] | contains("dotnet") or contains("csharp"))'
```text
#### From Smithery

```bash
# Query Smithery MCP catalog
https://smithery.ai/api/mcp-servers

# Filter by category
curl -s "https://smithery.ai/api/mcp-servers?category=development"
```text
### Local Discovery

#### From Package.json (Node.js projects)

```bash
# Check for MCP servers in devDependencies
jq '.devDependencies | keys[] | select(contains("mcp"))' package.json
```text
#### From Tool Configuration

```bash
# VS Code extensions
ls ~/.vscode/extensions/ | grep -i mcp

# Claude Code config
ls ~/.claude/ | grep -i mcp
```text
## Discovery Script

Add to hooks or commands:

```bash
# Inline mcp-discovery.sh: Discover available MCP servers
set -uo pipefail

DISCOVERED_MCP="{}"

# 1. Check official MCP registry (if network available)
if command -v curl >/dev/null 2>&1; then
  REGISTRY_DATA=$(curl -s --connect-timeout 5 \
    "https://registry.modelcontextprotocol.io/servers" 2>/dev/null || echo "")

  if [ -n "$REGISTRY_DATA" ]; then
    # Parse registry data
    DISCOVERED_MCP=$(echo "$REGISTRY_DATA" | jq -c '{registry: .servers}' 2>/dev/null || echo "{}")
  fi
fi

# 2. Check local MCP installations
LOCAL_MCPS="{}"

# Check for common MCP servers
if command -v uvx >/dev/null 2>&1; then
  LOCAL_MCPS=$(echo "$LOCAL_MCPS" | jq '. + {uvx: true}')
fi

if command -v npx >/dev/null 2>&1; then
  LOCAL_MCPS=$(echo "$LOCAL_MCPS" | jq '. + {npx: true}')
fi

# 3. Merge and return
echo "{\"discovered\": $DISCOVERED_MCP, \"local\": $LOCAL_MCPS}"

exit 0
```text
## MCP Compatibility Matrix

| MCP Server        | Type  | .NET Support | Status | Notes             |
| ----------------- | ----- | ------------ | ------ | ----------------- |
| serena            | stdio | Full         | Active | Semantic analysis |
| context7          | http  | Full         | Active | Library docs      |
| mcp-windbg        | stdio | Windows      | Active | Debugging         |
| microsoftdocs-mcp | http  | Full         | Active | MS Learn          |
| deepwiki          | http  | Full         | Active | Wiki search       |
| github            | http  | Full         | New    | GitHub API        |

## Auto-Configuration

### Update mcp.json

```bash
# Discover and update config
dotnet-agent-harness:mcp-discovery:update-config

# This appends to .rulesync/mcp.json:
{
  "mcpServers": {
    "newly-discovered-mcp": {
      "type": "stdio",
      "command": "uvx",
      "args": ["--from", "some-mcp", "server"]
    }
  }
}
```text
### Integration with rulesync

Add discovery to rulesync workflow:

```json
{
  "sources": [{ "source": "mcp://registry.modelcontextprotocol.io" }]
}
```text
## Best Practices

### 1. Prefer stdio for Local Tools

Local tools should use stdio transport:

- Better performance (no HTTP overhead)
- Works offline
- Easier debugging

### 2. Use http for Cloud Services

Cloud APIs should use http transport:

- No local installation required
- Automatic updates
- Scalable

### 3. Validate Before Adding

Always validate discovered MCPs:

```bash
# Check health before adding
[mcp-health:check <new-mcp>]

# Test functionality
[mcp-discovery:test <new-mcp>]
```text
### 4. Version Pinning

Pin MCP server versions for stability:

```json
{
  "mcpServers": {
    "some-mcp": {
      "type": "stdio",
      "command": "uvx",
      "args": ["--from", "some-mcp==1.2.3", "server"]
    }
  }
}
```text\n\n## Troubleshooting Discovery

### No MCPs Found

**Problem:** Registry unreachable

**Solutions:**

- Check network connectivity
- Try offline mode with cached registry
- Use local MCP servers only

### Incompatible MCPs

**Problem:** MCP doesn't support .NET

**Solutions:**

- Check MCP documentation for language support
- Look for .NET-specific alternatives
- Use HTTP MCPs (language-agnostic)

### Duplicate MCPs

**Problem:** Same MCP from multiple sources

**Solutions:**

- Prioritize official registry
- Use version comparison
- User confirmation for duplicates

## Integration Example

### Session Start Hook

Add MCP discovery to session start:

```json
{
  "hooks": {
    "sessionStart": [
      {
        "type": "command",
        "command": "# Discover MCP servers\necho 'Checking for available MCP servers...'\n# Discovery logic here",
        "timeout": 10
      }
    ]
  }
}
```text
### Notification

Inform user of discovered MCPs:

```text
Discovered MCP servers:
✓ serena (local)
✓ microsoftdocs-mcp (cloud)
✓ github (remote)

Run [mcp-discovery:update-config] to add new MCPs.
```text
## References

- [MCP Registry](https://registry.modelcontextprotocol.io/)
- [Smithery MCP Catalog](https://smithery.ai/)
- [skill:mcp-health] -- validate discovered MCPs
- [skill:dotnet-observability] -- metrics for discovery process
````
