# act Quick Reference

**Installation:** ✅ Installed at `~/.local/bin/act`

## Common Commands

```bash
# List all workflows
act -l

# PR validation workflow (most common during development)
act pull_request -W .github/workflows/pr-validation.yml

# Run specific jobs
act -j lint-format          # Format check only
act -j build                # Build only  
act -j test                 # Tests only

# Run multiple jobs
act -j lint-format -j build

# Dry run (see what would execute)
act -n
act -j build -n

# Verbose output
act -v
act -j build -v

# CI workflow
act push -W .github/workflows/ci.yml

# Skip jobs that require GitHub API
act pull_request --skip validate-title --skip pr-summary
```

## Fast Development Loop

```bash
# 1. Make code changes
# 2. Quick format check (30 seconds)
act -j lint-format -W .github/workflows/pr-validation.yml

# 3. Run build if format passes (2-3 minutes)
act -j build -W .github/workflows/pr-validation.yml

# 4. Run tests if build passes (5-10 minutes)
act -j test -W .github/workflows/pr-validation.yml
```

## Troubleshooting

```bash
# Check Docker is running
docker ps

# Pre-pull image manually if slow
docker pull catthehacker/ubuntu:act-latest

# Clean up containers
docker ps -a | grep act- | awk '{print $1}' | xargs docker rm

# Maximum verbosity
act -v -v -v
```

## Configuration

- **Config file:** `.actrc` (in repo root)
- **Secrets:** Create `.secrets` file (already in `.gitignore`)
- **Documentation:** `.github/LOCAL_TESTING.md`

## Notes

- First run downloads ~1-2 GB Docker image (one-time)
- Use `--reuse` flag in `.actrc` for faster subsequent runs
- Some jobs require GitHub API and may not work locally (skip with `--skip <job-id>`)
- Always test locally before pushing to save GitHub Actions minutes

---

**Full Documentation:** [.github/LOCAL_TESTING.md](.github/LOCAL_TESTING.md)
