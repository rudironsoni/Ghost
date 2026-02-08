# Local GitHub Actions Testing with `act`

This document describes how to test GitHub Actions workflows locally using [`act`](https://github.com/nektos/act), eliminating the need to push commits and consume GitHub Actions minutes during development.

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Common Commands](#common-commands)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Advanced Usage](#advanced-usage)
- [Limitations](#limitations)

---

## Installation

### Option 1: Using the Install Script (Recommended)

```bash
curl -s https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash
```

### Option 2: Manual Installation

1. Download the latest release for your platform:
   ```bash
   wget https://github.com/nektos/act/releases/latest/download/act_Linux_x86_64.tar.gz
   ```

2. Extract and install:
   ```bash
   tar -xzf act_Linux_x86_64.tar.gz
   sudo mv act /usr/local/bin/
   ```

3. Verify installation:
   ```bash
   act --version
   ```

### Prerequisites

- **Docker**: `act` runs workflows in Docker containers. Ensure Docker is installed and running:
  ```bash
  docker --version
  docker ps
  ```

---

## Quick Start

1. **List available workflows and jobs:**
   ```bash
   act -l
   ```

2. **Run a specific job:**
   ```bash
   act -j build
   ```

3. **Simulate a pull request event with mock data:**
   ```bash
   act pull_request -e .github/test-events/pr-event.json
   ```

4. **Run with verbose output:**
   ```bash
   act -v
   ```

---

## Testing Pull Request Workflows

The PR validation workflow requires PR context. Use the provided mock event files:

### Run PR Title Validation

```bash
# Run with mock PR event
act -j validate-title -e .github/test-events/pr-event.json

# Or set PR_TITLE environment variable
act -j validate-title --env PR_TITLE="feat: my feature"
```

### Run Lint and Format Check

```bash
# Run formatting check (handles line endings automatically)
act -j lint-format
```

### Run Full PR Validation

```bash
# Run all PR validation jobs (some may be skipped due to dependencies)
act pull_request -e .github/test-events/pr-event.json

# Or run specific jobs in sequence
act -j validate-title -e .github/test-events/pr-event.json
act -j lint-format
act -j build
```

---

## Common Commands

### List All Workflows and Jobs

```bash
act -l
```

**Example Output:**
```
Stage  Job ID              Job name              Workflow name         Workflow file         Events
0      validate-title      Validate PR Title     PR Validation         pr-validation.yml     pull_request
0      lint-format         Lint & Format Check   PR Validation         pr-validation.yml     pull_request
0      build               Build Solution        PR Validation         pr-validation.yml     pull_request
1      test                Run Unit Tests        PR Validation         pr-validation.yml     pull_request
```

### Run Specific Jobs

```bash
# Run single job
act -j build

# Run multiple jobs
act -j lint-format -j build

# Run all jobs in a workflow file
act -W .github/workflows/pr-validation.yml
```

### Simulate Different Events

```bash
# Pull request
act pull_request

# Push event
act push

# Workflow dispatch
act workflow_dispatch
```

### Dry Run (Show What Would Execute)

```bash
act -n
act -j build -n
```

### Run with Secrets

```bash
# Pass secrets via command line
act -s GITHUB_TOKEN=ghp_xxxxx

# Use .env file (create .env with KEY=VALUE pairs)
act --env-file .env

# Use .secrets file (same format as .env)
act --secret-file .secrets
```

---

## Configuration

### Project Configuration (`.actrc`)

The `.actrc` file in the repository root contains default settings for `act`:

```bash
# View current configuration
cat .actrc
```

**Key configurations:**
- **Docker images**: Uses `catthehacker/ubuntu:act-latest` for faster execution
- **Container options**: Bind mounts workspace, enables Docker-in-Docker
- **Reuse containers**: Speeds up subsequent runs with `--reuse`

### User Configuration

You can create a global config at `~/.actrc`:

```bash
# Example: Set GITHUB_TOKEN globally
echo '-s GITHUB_TOKEN=ghp_xxxxx' >> ~/.actrc
```

### Secrets Management

For sensitive values (tokens, credentials):

1. Create `.env` or `.secrets` file (already in `.gitignore`):
   ```bash
   echo "GITHUB_TOKEN=ghp_xxxxx" >> .secrets
   ```

2. Reference in `.actrc`:
   ```bash
   echo '--secret-file .secrets' >> .actrc
   ```

---

## Troubleshooting

### Common Issues

#### 1. **Docker Permission Denied**

**Error:**
```
Got permission denied while trying to connect to the Docker daemon socket
```

**Fix:**
```bash
# Add user to docker group
sudo usermod -aG docker $USER

# Log out and back in, or run:
newgrp docker
```

#### 2. **Image Pull Failures**

**Error:**
```
Error: Cannot pull image: ...
```

**Fix:**
```bash
# Pre-pull the required image
docker pull catthehacker/ubuntu:act-latest

# Or use a different image in .actrc
-P ubuntu-latest=node:16-buster-slim
```

#### 3. **Workflow Not Found**

**Error:**
```
Error: unable to read workflow
```

**Fix:**
- Ensure you're in the repository root
- Check workflow file syntax: `act -l`
- Validate YAML: `yamllint .github/workflows/*.yml`

#### 4. **Missing Global JSON**

**Error:**
```
Could not find global.json
```

**Fix:**
- Ensure `global.json` exists in repository root
- Check that `.actrc` bind mount is working

#### 5. **NuGet Restore Failures**

**Issue:** Slow NuGet restores on every run

**Fix:**
```bash
# Mount NuGet cache into container
act --container-options "-v $HOME/.nuget/packages:/root/.nuget/packages"
```

### Enable Debug Logging

```bash
# Maximum verbosity
act -v -v -v

# Show Docker commands being executed
act --verbose
```

### Clean Up Containers

```bash
# Remove stopped act containers
docker ps -a | grep act- | awk '{print $1}' | xargs docker rm

# Remove act images
docker images | grep act | awk '{print $3}' | xargs docker rmi
```

---

## Advanced Usage

### Testing Specific Workflows

#### PR Validation Workflow

```bash
# Run full PR validation with mock event
act pull_request -e .github/test-events/pr-event.json

# Run only build and test
act -j build -j test

# Run PR title validation with custom title
act -j validate-title --env PR_TITLE="fix(core): resolve memory leak"

# Test formatting
act -j lint-format
```

#### CI Workflow

```bash
# Run CI on push event with mock data
act push -e .github/test-events/push-event.json -W .github/workflows/ci.yml
```

#### Nightly Workflow

```bash
# Trigger scheduled workflow
act schedule -W .github/workflows/nightly.yml

# Or use workflow_dispatch
act workflow_dispatch -W .github/workflows/nightly.yml
```

### Matrix Testing

To test matrix strategies locally:

```bash
# Run all matrix combinations
act -j test --matrix dotnet-version:9.0 --matrix os:ubuntu-latest

# Test single matrix cell
act -j test --matrix dotnet-version:9.0
```

### Custom Event Payloads

Mock event files are provided in `.github/test-events/`:
- `pr-event.json` - Pull request event with sample PR data
- `push-event.json` - Push event with sample commit data

```bash
# Use provided mock PR event
act pull_request -e .github/test-events/pr-event.json

# Use provided mock push event
act push -e .github/test-events/push-event.json

# Create custom event payload
cat > custom-event.json << 'EOF'
{
  "pull_request": {
    "number": 456,
    "title": "feat(platforms): add Indeed scraper",
    "base": {"sha": "abc123"},
    "head": {"sha": "def456"}
  }
}
EOF

# Run with custom payload
act pull_request -e custom-event.json
```

### Platform-Specific Testing

```bash
# Test on specific platform
act -P ubuntu-latest=ubuntu:22.04

# Test with .NET SDK image
act -P ubuntu-latest=mcr.microsoft.com/dotnet/sdk:9.0
```

---

## Limitations

### Known Limitations

1. **GitHub Context**: Limited access to GitHub API and context variables
   - `github.token` may not work for API calls
   - `github.event` context is simulated
   - Repository metadata may be incomplete
   - **Workaround**: Use `.github/test-events/*.json` files for mock data

2. **Third-Party Actions**: Some actions may not work locally
   - `actions/github-script@v7`: Limited GitHub API access (but PR title validation now works with fallback)
   - `dorny/test-reporter@v1`: Requires GitHub API
   - `actions/cache@v4`: Works but cache is container-local

3. **Performance**: First run downloads Docker images (~1-2 GB)
   - Use `--reuse` flag to speed up subsequent runs
   - Consider using `--pull=false` after initial setup

4. **Secret Management**: `.secrets` file is not encrypted
   - Never commit secrets to version control
   - Use environment variables or secret managers for sensitive data

5. **Line Endings**: Container may use different line endings than host
   - **Fixed**: Workflow now normalizes line endings automatically
   - `.gitattributes` enforces LF line endings for all text files

### Workarounds

#### GitHub API and Context Issues

The workflow has been updated to handle missing GitHub context:

```bash
# PR title validation now works with mock events
act -j validate-title -e .github/test-events/pr-event.json

# Or use environment variable
act -j validate-title --env PR_TITLE="feat: my feature"

# If both are missing, uses test default: "test: default title for local testing"
act -j validate-title
```

#### Skip Jobs That Require GitHub API

```bash
# Skip jobs that won't work locally
act --skip pr-summary
```

#### Mock GitHub API Responses

Use the provided mock event files or create custom ones:

```bash
# Use provided mock events
act pull_request -e .github/test-events/pr-event.json
act push -e .github/test-events/push-event.json

# Set environment variables
act -s GITHUB_TOKEN=mock_token --env GITHUB_REPOSITORY=rudironsoni/Ghost
```

#### Use Conditional Execution

Workflows now handle act testing gracefully with fallbacks:

```yaml
# Example: PR title validation with fallback
- name: Check PR Title
  uses: actions/github-script@v7
  with:
    script: |
      const title = context.payload.pull_request?.title 
        || process.env.PR_TITLE 
        || 'test: default title for local testing';
      // validation logic...
```

---

## Best Practices

1. **Use `.actrc` for Team Consistency**: Commit `.actrc` to ensure all developers use the same configuration

2. **Fast Feedback Loop**: Run specific jobs during development:
   ```bash
   # Quick validation during development
   act -j lint-format
   act -j build
   
   # With mock PR event for title validation
   act -j validate-title -e .github/test-events/pr-event.json
   ```

3. **Pre-Push Validation**: Run full workflow before pushing:
   ```bash
   # Full PR validation
   act pull_request -e .github/test-events/pr-event.json
   
   # Or run key jobs
   act -j lint-format && act -j build && act -j test
   ```

4. **Cache NuGet Packages**: Mount host NuGet cache for faster restores:
   ```bash
   # Add to .actrc or run directly
   act --container-options "-v $HOME/.nuget/packages:/root/.nuget/packages"
   ```

5. **Clean Builds**: Remove `--reuse` flag occasionally for clean environment:
   ```bash
   act -j build --rm
   ```

---

## Resources

- [act GitHub Repository](https://github.com/nektos/act)
- [act Documentation](https://nektosact.com/)
- [Docker Images for act](https://github.com/catthehacker/docker_images)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

---

## Getting Help

If you encounter issues:

1. Check this troubleshooting guide
2. Run with verbose logging: `act -v`
3. Validate workflow syntax: `act -l`
4. Check Docker logs: `docker logs <container_id>`
5. Open an issue: [Ghost Issues](https://github.com/rudironsoni/Ghost/issues)

---

**Last Updated:** 2026-02-08
