# GitHub Configuration

This directory contains GitHub Actions workflows and related configuration for the Ghost project.

## Contents

- **`workflows/`** - GitHub Actions workflow definitions
  - `ci.yml` - Main integration workflow (build, test, package)
  - `pr-validation.yml` - Pull request validation checks
  - `nightly.yml` - Nightly builds and extended tests
  - `release.yml` - Release automation workflow
  - `security-scan.yml` - Security scanning and compliance

- **`LOCAL_TESTING.md`** - Guide for testing workflows locally with `act`

## Local Testing

Before pushing commits, you can test workflows locally using [`act`](https://github.com/nektos/act):

```bash
# List all workflows
act -l

# Test PR validation
act pull_request -W .github/workflows/pr-validation.yml

# Run specific job
act -j lint-format
```

**See [LOCAL_TESTING.md](LOCAL_TESTING.md) for complete documentation.**

## Workflow Overview

### CI Workflow (`ci.yml`)
Runs on every push to `main` and can be triggered manually:
- Build solution
- Run unit, integration, and E2E tests
- Security scans (CodeQL, secrets, dependencies)
- Generate SBOM
- Package NuGet artifacts

### PR Validation (`pr-validation.yml`)
Runs on every pull request to `main`:
- Validate PR title (conventional commits format)
- Check code formatting
- Build solution
- Run unit tests
- Check for deprecated DotnetSpider references

### Nightly Build (`nightly.yml`)
Runs daily at 02:00 UTC:
- Performance benchmarks
- Stability tests
- Full code coverage analysis
- Full integration test suite
- E2E test suite

### Release (`release.yml`)
Runs on version tags or manual trigger:
- Calculate semantic version
- Build and test
- Create NuGet packages
- Build and sign Docker images
- Publish to NuGet.org
- Create GitHub release

### Security Scan (`security-scan.yml`)
Runs daily and on push:
- Container image scanning
- SBOM generation
- License compliance checks
- CodeQL SAST analysis
- Dependency vulnerability scanning
- Secret detection

## Contributing

When adding or modifying workflows:

1. **Test locally first** using `act` (see [LOCAL_TESTING.md](LOCAL_TESTING.md))
2. **Follow best practices**:
   - Use pinned action versions (`@v4` not `@main`)
   - Add timeout limits to jobs
   - Use caching for dependencies
   - Add descriptive job and step names
3. **Document changes** in this README if adding new workflows

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Workflow Syntax Reference](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)
- [Local Testing with act](LOCAL_TESTING.md)
