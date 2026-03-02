---
description:
  'Search skills by keywords, tags, categories, or semantic similarity. Filter by platform, topic, or usage patterns.'
targets: ['*']
---

# /dotnet-agent-harness:search

Find skills by keyword, category, or functionality within the dotnet-agent-harness toolkit.

## Usage

```bash
/dotnet-agent-harness:search <query> [options]
```

## Parameters

- `query`: Search terms (keywords, tags, or natural language description)
- `--category`: Filter by category (e.g., `ui`, `data`, `testing`, `cloud`)
- `--platform`: Filter by target platform (e.g., `claudecode`, `opencode`, `copilot`)
- `--tag`: Filter by specific tag
- `--limit`: Maximum results (default: 20)

## Examples

```bash
# Search for skills related to authentication
/dotnet-agent-harness:search authentication

# Find all Blazor-related skills
/dotnet-agent-harness:search blazor --category ui

# Search for testing skills compatible with OpenCode
/dotnet-agent-harness:search "xunit testing" --platform opencode --tag testing

# Find skills for database operations
/dotnet-agent-harness:search "entity framework" --category data
```

## Output

Results include:

- Skill name and description
- Categories and tags
- Platform compatibility
- Usage examples

## Implementation

This command searches:

1. Skill names and descriptions
2. Tags and categories
3. Trigger phrases
4. Knowledge source references

Results are ranked by relevance and frequency of use.
