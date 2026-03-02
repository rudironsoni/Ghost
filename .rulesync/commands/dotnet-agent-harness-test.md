---
description: 'Test skills against defined test cases. Validate skill logic, output format, and cross-references.'
targets: ['*']
---

# /dotnet-agent-harness:test

Validate skill functionality with unit tests.

## Usage

```bash
/dotnet-agent-harness:test <skill-name> [options]
```

## Parameters

- `skill-name`: Name of skill to test (or `all` for all skills)
- `--verbose`: Show detailed test output
- `--format`: Output format (`text`, `json`, `junit`)
- `--fail-fast`: Stop on first failure

## Examples

```bash
# Test a specific skill
/dotnet-agent-harness:test dotnet-csharp-coding-standards

# Run all tests with JSON output
/dotnet-agent-harness:test all --format json

# Test with detailed output
/dotnet-agent-harness:test dotnet-efcore-patterns --verbose

# Test for CI/CD
/dotnet-agent-harness:test all --format junit --fail-fast
```

## Test Cases

Skills can include test cases in `test-cases/`:

```yaml
# .rulesync/skills/dotnet-example/test-cases/001-basic.yml
name: 'Basic validation'
input:
  query: 'How should I name private fields?'
expected_output_contains:
  - '_camelCase'
  - 'private fields'
validation:
  - pattern: "_\\w+"
    description: 'Should suggest underscore prefix'
```

## Output

```text
Testing dotnet-csharp-coding-standards...
✓ Frontmatter validation
✓ Cross-reference check
✓ Test case 1: Basic validation
✓ Test case 2: Edge cases

3 passed, 0 failed
```

## Exit Codes

- `0`: All tests passed
- `1`: One or more tests failed
- `2`: Invalid skill name or configuration error
