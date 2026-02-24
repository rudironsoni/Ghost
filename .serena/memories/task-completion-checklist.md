# Ghost Project - Task Completion Checklist

## Verification Requirements

Before claiming work complete, ensure ALL of the following pass:

### 1. Code Quality Checks
- [ ] `dotnet format Ghost.sln --verify-no-changes` passes
- [ ] `dotnet build Ghost.sln --no-restore --warnaserror` succeeds
- [ ] No analyzer warnings

### 2. Test Verification
- [ ] `dotnet test Ghost.sln --no-build` passes
  - OR `dotnet test Ghost.sln` if no build artifacts exist
- [ ] All new functionality has appropriate test coverage

### 3. Git Workflow
- [ ] `git status` - Review what changed
- [ ] `git add <files>` - Stage only intended changes
- [ ] `bd sync` - Sync beads changes
- [ ] `git commit -m "..."` - Commit with descriptive message
- [ ] `bd sync` - Sync any new beads changes
- [ ] `git push` - Push to remote

### 4. Issue Management
- [ ] Update issue with verification evidence
- [ ] Close completed issue with `bd close <id>`
- [ ] Link related issues/epics in commit message

## Commit Message Format
```
type(scope): brief description

Longer description if needed...

Build: ✓ Passes/Fails
Tests: ✓ Pass/Fail

Related: Ghost-<id>, Ghost-<epic-id>
```

## Session Close Protocol (CRITICAL)

**NEVER skip this.** Work is not done until pushed.

```bash
# 1. Check what changed
git status

# 2. Stage code changes
git add <files>

# 3. Commit beads changes (if any)
bd sync

# 4. Commit code
git commit -m "type(scope): description"

# 5. Commit any new beads changes
bd sync

# 6. Push to remote
git push
```

## Beads Workflow Reminders
- Use `bd` for ALL task tracking (NOT TodoWrite or markdown files)
- Create beads issue BEFORE writing code
- Mark `in_progress` when starting work
- Run `bd ready` to find available work
- Run `bd sync` at session end

## dotnet-skills Reference
When working on .NET code, prefer retrieval-led reasoning:
1. Skim repo patterns
2. Consult dotnet-skills by name
3. Implement smallest-change
4. Note conflicts

Common skills to invoke:
- `modern-csharp-coding-standards`
- `csharp-concurrency-patterns`
- `api-design`
- `type-design-performance`
- `efcore-patterns`
- `dotnet-resilience`
