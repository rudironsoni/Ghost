# Ghost Project - Suggested Commands

## Project Overview
Ghost is a C# .NET 10 monorepo for a stealth browser automation framework for job platform scraping.

## Essential Development Commands

### Build Commands
```bash
# Restore dependencies
dotnet restore Ghost.sln

# Build entire solution (Release)
dotnet build Ghost.sln --configuration Release --no-restore

# Build with warnings as errors (CI style)
dotnet build Ghost.sln --configuration Release --no-restore --warnaserror -p:NoWarn=CA1016

# Clean build artifacts
dotnet clean Ghost.sln
```

### Test Commands
```bash
# Run all tests
dotnet test Ghost.sln --configuration Release

# Run with verbose output
dotnet test Ghost.sln --configuration Release --verbosity normal

# Run specific test project
dotnet test tests/Kernel/Ghost.Kernel.UnitTests --configuration Release

# Run tests matching a filter
dotnet test Ghost.sln --configuration Release --filter "FullyQualifiedName~RedisJobDispatcher"
```

### Code Quality Commands
```bash
# Format check
dotnet format Ghost.sln --verify-no-changes

# Apply formatting
dotnet format Ghost.sln

# Run analyzers
dotnet build Ghost.sln --warnaserror
```

### Task Tracking Commands (Beads)
```bash
# Find available work
bd ready

# View issue details
bd show <id>

# Start work on an issue
bd update <id> --status in_progress

# Complete work
bd close <id>

# Sync with git remote
bd sync
```

### Git Commands
```bash
# Check status
git status

# Stage changes
git add <files>

# Commit changes
git commit -m "message"

# Push to remote
git push
```

### Session Close Protocol (CRITICAL)
Before claiming work complete, ALWAYS run:
```bash
# 1. Check what changed
git status

# 2. Stage code changes
git add <files>

# 3. Commit beads changes
bd sync

# 4. Commit code
git commit -m "..."

# 5. Commit any new beads changes
bd sync

# 6. Push to remote
git push
```

## Key Project Files
- `Ghost.sln` - Solution file
- `Directory.Build.props` - Shared MSBuild properties
- `Directory.Packages.props` - Central Package Management (CPM)
- `.editorconfig` - Code style enforcement
- `CLAUDE.md` (or `AGENTS.md`) - Project guidance
- `tests/.runsettings` - Test run configuration
