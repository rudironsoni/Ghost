Contributing to Ghost

Thank you for your interest in contributing to Ghost. This document explains
the process for contributing code, reporting bugs, proposing features, and
submitting pull requests.

Getting started

- Fork the repository and create a branch for your change.
- Ensure your branch name clearly identifies the work (e.g. feat/feature-name,
  fix/issue-number-short-description).
- Keep changes small and focused.

Development setup

Provide a brief summary of the recommended development environment and common
commands. For example:

- Install dependencies: dotnet restore or npm install (depending on project)
- Build: dotnet build or npm run build
- Test: dotnet test or npm test

Coding standards

- Follow existing code style and naming conventions used in the repository.
- Write clear, concise commit messages. See commit message guidelines below.
- Add unit tests for new logic and ensure existing tests pass.

Submitting changes

1. Update or add tests that cover your changes.
2. Run the test suite and ensure all tests pass locally.
3. Update documentation as needed (README, docs, etc.).
4. Create a pull request against the main branch with a clear description of
   the change and the motivation.

Pull request process

- Provide a clear title and description explaining the problem and the change.
- Link related issues using "Fixes #<issue-number>" when applicable.
- Include screenshots, logs, or recordings for UI or behavioral changes.
- Ensure CI passes and address review feedback promptly.

Issue reporting

When opening an issue, include:

- A descriptive title and a clear description of the problem
- Steps to reproduce the issue
- Expected and actual behavior
- Environment information (OS, runtime versions, etc.)
- Logs or screenshots where helpful

Security disclosures

If you discover a security vulnerability, do not create a public issue. See
SECURITY.md for how to report security issues privately.

Commit message guidelines

Use short, present-tense subject lines. Examples:

- feat: add X feature
- fix: correct Y behavior
- docs: update README

License

By contributing, you agree that your contributions will be licensed under the
project's existing license.

Thank you for helping improve Ghost!
