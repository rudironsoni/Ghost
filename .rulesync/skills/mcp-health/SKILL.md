---
name: mcp-health
description: >-
  Validates MCP server connectivity and health. Checks stdio and http MCP servers for availability, reports connection
  status, and provides fallback guidance. Triggers on: mcp health, check mcp, validate mcp, mcp status, mcp
  connectivity.
targets: ['*']
tags: ['dotnet', 'skill', 'mcp', 'monitoring']
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
opencode:
  mode: 'primary'
  tools:
    bash: true
    edit: false
    write: false
copilot:
  tools: ['read', 'search', 'execute']
---

# MCP Health

Validates MCP server connectivity and health status for local stdio and remote http MCP servers.

## Preloaded Skills

Always load before analysis:

- [skill:dotnet-advisor] -- routing and context

## Usage

```bash
# Check all MCP servers
[mcp-health:check-all]

# Check specific MCP
[mcp-health:check microsoftdocs-mcp]

# Get health report
[mcp-health:report]
```

## MCP Server Types

### stdio Servers (Local)

Launch local processes via stdio transport:

- **serena** -- Semantic code analysis (Language Server Protocol)
- **context7** -- Library documentation lookup
- **mcp-windbg** -- Windows debugging tools

### http Servers (Remote)

Connect to remote APIs via HTTP:

- **microsoftdocs-mcp** -- Microsoft Learn documentation
- **deepwiki** -- DeepWiki MCP server
- **github** -- GitHub API operations

## Health Check Methods

### For stdio Servers

```bash
# Check if command exists and is executable
which uvx || which npx

# Test stdio connection
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | \
  uvx --from serena-mcp server 2>&1 | head -5
```

### For http Servers

```bash
# Test HTTP connectivity
curl -s -o /dev/null -w "%{http_code}" https://learn.microsoft.com/api/mcp/health
curl -s -o /dev/null -w "%{http_code}" https://mcp.deepwiki.com/health
```

## Validation Script

Add to session start hooks:

```bash
# Inline mcp-health-check.sh: Validate MCP servers
set -uo pipefail

MCP_CONFIG="${MCP_CONFIG:-.rulesync/mcp.json}"
[ ! -f "$MCP_CONFIG" ] && exit 0

# Check if jq is available
if ! command -v jq >/dev/null 2>&1; then
  echo "{\"warning\":\"jq not found - skipping MCP health check\"}"
  exit 0
fi

# Extract and validate MCP servers
FAILED_MCP=""

while IFS= read -r mcp_name; do
  mcp_type=$(jq -r ".mcpServers.\"$mcp_name\".type" "$MCP_CONFIG" 2>/dev/null || echo "")

  case "$mcp_type" in
    http)
      url=$(jq -r ".mcpServers.\"$mcp_name\".url" "$MCP_CONFIG" 2>/dev/null)
      if [ -n "$url" ]; then
        if ! curl -s --connect-timeout 5 -o /dev/null "$url/health" 2>/dev/null; then
          FAILED_MCP="$FAILED_MCP $mcp_name"
        fi
      fi
      ;;
    stdio)
      # Check if command exists
      cmd=$(jq -r ".mcpServers.\"$mcp_name\".command" "$MCP_CONFIG" 2>/dev/null)
      if ! command -v "$cmd" >/dev/null 2>&1; then
        # Check common package managers
        case "$cmd" in
          uvx) command -v uvx || command -v pipx || FAILED_MCP="$FAILED_MCP $mcp_name" ;;
          npx) command -v npx || FAILED_MCP="$FAILED_MCP $mcp_name" ;;
        esac
      fi
      ;;
  esac
done < <(jq -r '.mcpServers | keys[]' "$MCP_CONFIG" 2>/dev/null || true)

if [ -n "$FAILED_MCP" ]; then
  echo "{\"warning\":\"MCP servers unavailable:$FAILED_MCP\"}"
else
  echo "{\"status\":\"healthy\"}"
fi

exit 0
```

## Health Report Format

```json
{
  "timestamp": "2026-02-28T10:45:00Z",
  "servers": {
    "serena": {
      "type": "stdio",
      "status": "healthy",
      "command": "uvx",
      "available": true
    },
    "microsoftdocs-mcp": {
      "type": "http",
      "status": "healthy",
      "url": "https://learn.microsoft.com/api/mcp",
      "latency_ms": 45
    },
    "context7": {
      "type": "http",
      "status": "healthy",
      "url": "https://mcp.context7.com/mcp"
    }
  },
  "summary": {
    "total": 5,
    "healthy": 4,
    "unavailable": 1
  }
}
```

## Troubleshooting

### stdio Server Issues

**Problem:** Command not found (uvx, npx)

**Solutions:**

```bash
# Install uvx via pipx
pipx install uvx

# Install npx (comes with Node.js)
npm install -g npx

# Or use npm directly for npx packages
npm install -g @anthropic-ai/mcp-server-fetch
```

**Problem:** MCP server crashes immediately

**Solutions:**

- Check MCP server logs: `--verbose` flag
- Verify environment variables in MCP config
- Update MCP server: `pipx upgrade serena-mcp`

### http Server Issues

**Problem:** Connection timeout

**Solutions:**

- Check network connectivity: `curl -I <mcp-url>`
- Verify firewall/proxy settings
- Check MCP service status page

**Problem:** 401 Unauthorized

**Solutions:**

- Verify API tokens in `.env` or environment
- Check token expiration
- Regenerate tokens if needed

## Fallback Behavior

When MCP servers are unavailable:

1. **graceful degradation** -- Continue without MCP features
2. **local caching** -- Use cached MCP responses if available
3. **alternative sources** -- Fall back to web search or local docs
4. **user notification** -- Inform user of unavailable features

## Integration

### With Hooks

Add to `.rulesync/hooks.json`:

```json
{
  "hooks": {
    "sessionStart": [
      {
        "type": "command",
        "command": "# MCP health check script here",
        "timeout": 30
      }
    ]
  }
}
```

### With Skills

Reference in skills that depend on MCP:

```markdown
## Preloaded Skills

- [skill:mcp-health] -- validate MCP availability
- [skill:microsoft-learn-mcp] -- query Microsoft documentation
```

## References

- [MCP Specification](https://modelcontextprotocol.io/)
- [skill:dotnet-observability] -- for health metrics export
- [skill:dotnet-resilience] -- for retry patterns
