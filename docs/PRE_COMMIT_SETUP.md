# Pre-commit Hooks Setup

This document explains how to set up and use pre-commit hooks for the Ghost project. These hooks automatically validate your code **before** you commit, catching errors locally instead of consuming CI minutes.

## What Gets Validated

The pre-commit hooks run three main checks:

1. **Code Formatting** - Ensures your C# code matches `.editorconfig` rules
2. **Build Validation** - Compiles the solution with warnings treated as errors
3. **Quick Non-Smoke Tests** - Runs fast deterministic checks (excludes `Smoke` and `End2End`)

Plus standard file hygiene checks:
- Trailing whitespace removal
- End-of-file newline enforcement
- YAML/JSON syntax validation
- Large file detection (>1MB)
- Merge conflict marker detection

## Installation

### Prerequisites

- Python 3.8 or higher (check with `python3 --version`)
- .NET 10 SDK installed
- Git repository initialized

### Step 1: Install pre-commit

Using pip:
```bash
pip install pre-commit
```

Using pipx (recommended for isolated installation):
```bash
pipx install pre-commit
```

Using Homebrew (macOS/Linux):
```bash
brew install pre-commit
```

### Step 2: Install Git Hooks

From the repository root:
```bash
pre-commit install
```

You should see:
```
pre-commit installed at .git/hooks/pre-commit
```

### Step 3: Verify Installation

Run hooks on all files to verify everything works:
```bash
pre-commit run --all-files
```

## Usage

### Automatic Validation

Once installed, hooks run automatically when you commit:

```bash
git add src/Core/Ghost/GhostKernel.cs
git commit -m "feat: add new feature"
```

The hooks will run and either:
- ✅ **Pass** - Your commit proceeds normally
- ❌ **Fail** - The commit is blocked and you see which check failed

### Manual Validation

Run hooks without committing:

```bash
# Run all hooks on staged files
pre-commit run

# Run all hooks on all files
pre-commit run --all-files

# Run a specific hook
pre-commit run dotnet-format-check
pre-commit run dotnet-build
pre-commit run dotnet-test-quick
```

### Skipping Hooks (Emergency Only)

**⚠️ Use sparingly!** Only skip hooks when you have a good reason (e.g., work-in-progress checkpoint):

```bash
# Skip all hooks
git commit --no-verify -m "WIP: checkpoint"

# Better: Use git stash instead of committing broken code
git stash push -m "WIP: feature X"
```

### Updating Hooks

Pre-commit hooks are versioned. Update them periodically:

```bash
pre-commit autoupdate
```

## Troubleshooting

### "dotnet: command not found"

**Problem:** pre-commit can't find `dotnet` CLI.

**Solution:** Ensure .NET SDK is in your PATH:
```bash
which dotnet
# Should print a path like /usr/local/share/dotnet/dotnet
```

Add to your shell profile if missing:
```bash
export PATH="$PATH:/usr/local/share/dotnet"
```

### "No module named 'pre_commit'"

**Problem:** pre-commit isn't installed or not in PATH.

**Solution:** Reinstall and verify:
```bash
pip install --user --upgrade pre-commit
which pre-commit
```

### Format Check Fails

**Problem:** `dotnet-format-check` reports formatting violations.

**Solution:** Auto-fix with:
```bash
dotnet format Ghost.sln
```

Then re-stage your changes:
```bash
git add -u
git commit
```

### Build Fails with Warnings

**Problem:** `dotnet-build` fails because warnings are treated as errors.

**Solution:** Fix the warnings! The project enforces zero warnings. Common fixes:
- Add missing XML documentation comments
- Fix nullable reference warnings
- Remove unused variables/usings

### Tests Fail

**Problem:** `dotnet-test-quick` fails.

**Solution:** Run tests manually to see details:
```bash
dotnet test Ghost.sln --no-build --filter "Category!=Smoke&Category!=End2End&Capability!=RequiresProviderLive"
```

Fix the failing tests before committing.

### Hook Takes Too Long

**Problem:** Pre-commit hooks are slow.

**Explanation:** The hooks run a full build and test suite. This is intentional - it catches errors early.

**Options:**
1. **Recommended:** Wait for hooks (usually 30-60 seconds)
2. Stage smaller commits (faster to validate)
3. Skip hooks temporarily (`--no-verify`) and run CI validation

### Clean Hook State

If hooks behave strangely, clean and reinstall:

```bash
pre-commit uninstall
pre-commit clean
pre-commit install
pre-commit run --all-files
```

## Configuration

The hooks are configured in `.pre-commit-config.yaml` at the repository root.

### Customizing Hook Behavior

Edit `.pre-commit-config.yaml` to:
- Add/remove hooks
- Change test filters
- Adjust file patterns
- Modify stages (commit, push, etc.)

After changes, update hooks:
```bash
pre-commit install --install-hooks
```

### Disabling Specific Hooks

Temporarily disable a hook by commenting it out in `.pre-commit-config.yaml`:

```yaml
# - id: dotnet-test-quick  # Commented out
#   name: Quick unit tests
#   ...
```

## CI/CD Integration

Pre-commit hooks match CI validation:
- ✅ Same format check (`dotnet format --verify-no-changes`)
- ✅ Same build command (`dotnet build Ghost.sln --no-restore --warnaserror`)
- ✅ Same test filter (excludes Smoke/End2End/live-provider capability lane)

**Goal:** If pre-commit passes locally, CI should pass too.

## Performance Tips

1. **Commit smaller changesets** - Fewer files = faster validation
2. **Run `dotnet restore` once** - Pre-commit reuses restored packages
3. **Use `--no-verify` sparingly** - Skipping hooks defeats the purpose
4. **Keep tests fast** - Slow tests slow down commits

## Benefits

- 💰 **Save CI minutes** - Catch errors before pushing
- ⚡ **Faster feedback** - Know immediately if something breaks
- 🎯 **Enforce standards** - Code formatting and quality automatically checked
- 🚀 **Better commits** - Only working code gets committed
- 🛡️ **Team consistency** - Everyone runs the same checks

## Further Reading

- [pre-commit documentation](https://pre-commit.com/)
- [Ghost AGENTS.md](../AGENTS.md) - Build and test commands
- [.editorconfig](../.editorconfig) - Code formatting rules
