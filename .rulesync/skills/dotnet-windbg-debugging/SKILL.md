---
name: dotnet-windbg-debugging
description: Debugs Windows apps via WinDbg MCP. Crash, hang, high-CPU, and memory triage from dumps or live attach.
license: MIT
targets: ['*']
tags: ['foundation', 'dotnet', 'skill']
version: '0.0.1'
author: 'dotnet-harness'
invocable: true
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
codexcli:
  short-description: '.NET skill guidance for foundation tasks'
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
---

# dotnet-windbg-debugging

Windows user-mode debugging using WinDbg MCP tools. Applicable to any Windows application -- native, managed (.NET/CLR),
or mixed-mode. Guides investigation of crash dumps, application hangs, high CPU, and memory pressure through structured
command packs and report templates.

**Platform:** Windows only.

## Scope

- Crash dump analysis (.dmp files) for any Windows process (native, .NET, or mixed-mode)
- Live process attach via cdb debug server
- Hang and deadlock diagnosis (thread analysis, lock detection)
- High CPU triage (runaway thread identification)
- Memory pressure and leak investigation via native heap analysis
- Kernel dump triage (BSOD / bugcheck analysis)
- COM/RPC wait chain and UI message pump analysis
- Structured diagnostic reports with stack evidence

## Out of scope

- .NET SDK diagnostic tools (dotnet-counters, dotnet-trace, dotnet-dump) -- see [skill:dotnet-profiling]
- GC tuning and managed memory optimization -- see [skill:dotnet-gc-memory]
- Performance benchmarking and regression detection -- see [skill:dotnet-benchmarkdotnet]
- Application performance architecture patterns -- see [skill:dotnet-performance-patterns]

## MCP Tool Contract

Use these `mcp-windbg` operations:

1. `mcp_mcp-windbg_open_windbg_remote` -- attach to a live debug server
2. `mcp_mcp-windbg_open_windbg_dump` -- open a saved dump file
3. `mcp_mcp-windbg_run_windbg_cmd` -- execute debugger commands
4. `mcp_mcp-windbg_close_windbg_remote` -- detach from live session
5. `mcp_mcp-windbg_close_windbg_dump` -- close dump session

## Quick Start

1. Configure and access MCP: see `reference/access-mcp.md`
2. Choose workflow:
   - Live attach: see `reference/live-attach.md`
   - Dump analysis: see `reference/dump-workflow.md`
3. Choose a task reference: see `reference/scenario-command-packs.md`
4. Use support references as needed:
   - Sanity check: `reference/sanity-check.md`
   - Symbols: `reference/symbols.md`
   - Common patterns: `reference/common-patterns.md`
   - Capture playbooks: `reference/capture-playbooks.md`
5. Return results using `reference/report-template.md`
6. Close the debug session.

## Completion Criteria

- Correct mode selected (live or dump)
- Task reference executed for the symptom
- Findings reference concrete stacks/threads/modules
- Session closed cleanly

## Guardrails

- Do not claim certainty without callee-side evidence
- Do not call it a deadlock unless lock/wait evidence supports it
- Preserve user privacy: do not include secrets from environment blocks in reports

Cross-references: [skill:dotnet-profiling] for .NET SDK diagnostic tools, [skill:dotnet-gc-memory] for managed GC and
memory tuning.

## References

- [WinDbg MCP](https://github.com/anthropics/windbg-mcp) -- MCP server for WinDbg integration
- [WinDbg Documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/) -- Microsoft debugger
  documentation

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
