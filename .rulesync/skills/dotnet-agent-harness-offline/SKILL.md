---
name: dotnet-agent-harness-offline
description: 'Offline mode with local caching for air-gapped environments'
targets: ['*']
tags: ['harness', 'offline', 'cache']
version: '0.0.1'
author: 'dotnet-agent-harness'
license: MIT
invocable: true
---

# Offline Mode

Enable offline operation with local skill and MCP caching.

## Activation

````bash
# Set offline mode
export DOTNET_HARNESS_OFFLINE=true

# Or use flag
rulesync generate --offline
```text
## Cache Structure

```text
.dotnet-agent-harness/
├── cache/
│   ├── skills/          # Cached skill content
│   ├── mcp-responses/   # Cached MCP responses
│   └── registry/        # Cached registry data
└── index.json           # Search index
````

## Workflow

1. **Online Sync** (when connected):

   ```bash
   dotnet-agent-harness:sync-cache
   ```

2. **Offline Operation**:
   - Uses cached skills exclusively
   - MCP servers return cached responses
   - Search uses local index

## Configuration

```json
{
  "offline": {
    "enabled": false,
    "cachePath": ".dotnet-agent-harness/cache",
    "maxCacheAge": "7d",
    "fallbackEnabled": true
  }
}
```

## Commands

- `dotnet-agent-harness:sync-cache` - Sync cache from remote
- `dotnet-agent-harness:clear-cache` - Clear local cache
- `dotnet-agent-harness:cache-status` - Show cache statistics

## MCP Fallback

When offline:

- Serena: Uses cached symbol data
- Context7: Returns cached library docs
- Microsoft Learn: Serves from local cache

## Best Practices

1. Sync before travel/air-gapped work
2. Keep cache under 500MB
3. Clear stale cache weekly
4. Use selective caching for large projects

## Troubleshooting

**Cache Miss**: Run `dotnet-agent-harness:sync-cache` **Stale Data**: Clear and re-sync **Large Cache**: Exclude heavy
MCP servers

## Code Navigation (Serena MCP)

**Primary approach:** Use Serena symbol operations for efficient code navigation:

1. **Find definitions**: `serena_find_symbol` instead of text search
2. **Understand structure**: `serena_get_symbols_overview` for file organization
3. **Track references**: `serena_find_referencing_symbols` for impact analysis
4. **Precise edits**: `serena_replace_symbol_body` for clean modifications

**When to use Serena vs traditional tools:**

- **Use Serena**: Navigation, refactoring, dependency analysis, precise edits
- **Use Read/Grep**: Reading full files, pattern matching, simple text operations
- **Fallback**: If Serena unavailable, traditional tools work fine

**Example workflow:**

```text
# Instead of:
Read: src/Services/OrderService.cs
Grep: "public void ProcessOrder"

# Use:
serena_find_symbol: "OrderService/ProcessOrder"
serena_get_symbols_overview: "src/Services/OrderService.cs"
```
