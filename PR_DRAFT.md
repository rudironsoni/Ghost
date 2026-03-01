Summary
-------

This PR represents the work to migrate JA3 fingerprint hashing from MD5 to SHA-256 and related refactors completed in the repository. Key changes include:

- Entity metadata refactor: reorganized entity metadata and updated types to improve parsing and reduce invalid states.
- FileSystemDeadLetterQueue rename: renamed FileSystemDeadLetterQueue to FileSystemPoisonQueue (and adjusted usages) to better communicate semantics.
- JA3 MD5 -> SHA-256 migration: replaced MD5-based JA3 fingerprint generation with a SHA-256 implementation across the codebase, updated helpers, and removed permitted MD5 exceptions.
- Audit documentation: added audit notes describing the security rationale and CA5351 compliance considerations.

Commits Included
----------------

The following commits are included on this branch (most recent first):

<!-- commits -->


Rationale for JA3 MD5 -> SHA-256
--------------------------------

MD5 is considered cryptographically weak and is flagged by security analyzers (CA5351). Moving JA3 fingerprints to SHA-256 eliminates MD5 usage in telemetry and reduces collision risk. This migration aligns with organizational security policy and CA5351 compliance guidance.

Tests & Verification
--------------------

- Build: solution builds successfully (dotnet build)
- Tests: unit and integration test suites run and passed locally
- Linting/Analyzers: code analyzer warnings addressed; editorconfig updated to remove MD5 exceptions for JA3 helpers

Compatibility Impact & Migration Plan
------------------------------------

This change alters the JA3 fingerprint representation (MD5 -> SHA-256). Downstream consumers that store or compare JA3 values must be updated. Migration plan:

1. Deploy library update and version bump.
2. Run a compatibility layer that accepts both MD5 and SHA-256 for a period (if necessary).
3. Migrate persisted JA3 values in downstream stores to SHA-256 transformation using recorded network flows or re-computation where feasible.
4. After full rollout, remove MD5 compatibility layer.

Suggested Review Checklist
-------------------------

- Confirm all instances of JA3 generation now use SHA-256 and no MD5 APIs remain.
- Verify analyzer CA5351 warnings are resolved and no suppressed exceptions remain unintentionally.
- Ensure entity metadata changes preserve serialization contracts and versioning.
- Validate rename of FileSystemDeadLetterQueue to FileSystemPoisonQueue across consumers.
- Confirm tests cover both legacy and new fingerprint usages where applicable.

Files Changed
-------------

List of changed files included in this PR (non-exhaustive):

- src/**/JA3HashHelper.cs        (MD5 -> SHA256 implementation)
- src/**/EntityMetadata/**       (refactor files)
- src/**/FileSystemPoisonQueue.cs (rename)
- docs/ja3/audit.md              (audit notes)

Verification Notes
------------------

- Local build: dotnet build --configuration Release
- Tests: dotnet test --no-build --verbosity minimal
- Analyzer: dotnet format / run analyzers to confirm CA5351 no longer triggers for JA3 code

If anything in this PR requires follow-up (migration tooling, data fixups), please raise an issue with the tag "migration/ja3-sha256".
