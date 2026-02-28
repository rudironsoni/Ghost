# Deep Research Architecture

## Core Design Principles

1. **Separation of Concerns**: Each agent has a single, well-defined responsibility
2. **File-Based Persistence**: Findings written to disk, not kept in context
3. **Parallel Execution**: Research threads run concurrently
4. **Minimal Context**: Agents only see what they need to see

## Agent Roles

### Query Analyzer

- **Input**: User query
- **Output**: Research plan
- **Responsibility**: Decompose query into parallel research threads
- **Key Decision**: Complexity level (simple/moderate/complex)

### Research Agent

- **Input**: Single research thread from plan
- **Output**: Findings file
- **Responsibility**: Execute searches, compile findings
- **Parallelism**: N agents run simultaneously

### Synthesizer

- **Input**: All findings files
- **Output**: Final report
- **Responsibility**: Integrate findings, resolve conflicts, structure output
- **Key Decision**: Brief vs report based on complexity and consensus

## File Naming Convention

````text
research-plan.md              # Query Analyzer output
findings-{thread-name}.md     # Research Agent outputs
final-output.md               # Synthesizer output
```text

## Complexity Guidelines

| Complexity | Thread Count | When to Use                                 |
| ---------- | ------------ | ------------------------------------------- |
| Simple     | 2-3          | Single concept, clear answer expected       |
| Moderate   | 3-4          | Multiple aspects, some synthesis required   |
| Complex    | 5-6          | Multi-domain, conflicting viewpoints likely |

## Parallelism Strategy

**Always parallel where possible:**

- Research agents: Parallel (no dependencies between threads)
- Searches within agent: Parallel (independent sources)
- Analysis within agent: Sequential (builds on search results)

**Never parallel:**

- Query Analyzer must complete before research agents
- Synthesizer must wait for all research agents
- Each research agent's internal analysis (sequential by design)
````
