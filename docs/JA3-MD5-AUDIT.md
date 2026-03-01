# JA3 MD5 Audit & Governance Rationale

## Summary

JA3 is an industry specification for TLS ClientHello fingerprinting that defines a canonical string representation of specific ClientHello fields and mandates computing a 32-character lowercase MD5 hex digest of that string to form the JA3 fingerprint. This repository implements that deterministic JA3 fingerprint algorithm in a small, internal helper (src/Kernel/Ghost/Stealth/TLS/JA3HashHelper.cs) to interoperate with ecosystem tooling and detection systems that expect standard JA3 fingerprints.

## Security considerations

- MD5 is cryptographically broken for security-sensitive use cases: collisions have been practical since 2004 and MD5 must never be used for password hashing, signatures, HMACs, or other confidentiality/integrity protections.
- For JA3 fingerprinting, MD5's determinism and stable 128-bit output are used as a non-security identifier only. The threat surface is different: JA3 fingerprints are not secrets and are intended to be stable across implementations.
- Limits and mitigations:
  - Scope: MD5 usage is limited to a single internal helper and well-documented callsites to minimize accidental misuse.
  - Encapsulation: All JA3 MD5 operations are encapsulated in src/Kernel/Ghost/Stealth/TLS/JA3HashHelper.cs; callers receive only the hex string fingerprint.
  - Review: Any change to the helper or additional MD5 usages must undergo security review and explicit policy approval (see checklist below).
  - Monitoring: CI must continue to flag CA5351 (Do Not Use Broken Cryptographic Algorithms) as an error except when a formal, recorded exception is granted; this document provides the rationale to support a narrow, auditable exception process.

## Exact code locations using MD5

The following files contain intentional, auditable MD5 usage for JA3 or configuration referencing MD5:

- src/Kernel/Ghost/Stealth/TLS/JA3HashHelper.cs
- src/Kernel/Ghost/Stealth/TLS/JA3Profile.cs (calls the helper)

Search note: Grep for "MD5" or CA5351 warnings to find any additional occurrences. Any new MD5 usage must be evaluated and added to this document.

## Recommended controls for granting a narrow analyzer exception

If a team chooses to grant a narrow CA5351 analyzer exception (severity relaxation) for the JA3 helper, require the following governance controls:

- Security review checklist (must be completed and attached to the approval):
  1. Purpose justification: Explain why MD5 is required (interop/spec compliance) and why alternatives (SHA256, SHA1) are not acceptable for the fingerprint/token format.
  2. Scope minimization: Confirm the MD5 use is confined to a single internal helper and no sensitive data is passed to it.
  3. Implementation review: Confirm the helper uses runtime-provided MD5 APIs (no custom MD5 implementation), avoids handing raw byte arrays to sensitive APIs, and minimizes allocations.
  4. Threat analysis: Document risks (collisions, fingerprint spoofing) and why they are acceptable given the non-security usage, including any potential abuse scenarios.
  5. Acceptance criteria: Tests validating deterministic outputs for canonical inputs and code comments describing intent.

- Owner: Assign a code owner and security owner (e.g., @team/stealth, security@example.com) responsible for the exception and periodic reviews.

- Acceptance criteria (policy):
  - All usages of MD5 must be present only in the approved helper and called only from approved callsites (documented in PR).
  - Unit tests demonstrating canonical JA3 input -> expected MD5 hex output must exist.
  - A link to this audit document must be included in the granting PR/issue.

- Monitoring: Add a CI check that fails if new MD5 usages are added outside the approved helper (e.g., grep for "MD5.Create(" or "MD5.HashData(" in changed files) and require security approval for any findings.

- Re-evaluation period: Re-approve the exception annually or sooner if the JA3 specification changes or if new risk vectors are discovered.

## Suggested PR/issue template text to request policy approval

Title: Request: Narrow CA5351 exception for JA3 MD5 helper

Body:

- Summary: We request a narrow analyzer exception to permit MD5 usage in src/Kernel/Ghost/Stealth/TLS/JA3HashHelper.cs to compute official JA3 fingerprints. This is necessary for interoperability with JA3-consuming systems which expect an MD5 32-character hex fingerprint.
- Files: src/Kernel/Ghost/Stealth/TLS/JA3HashHelper.cs, src/Kernel/Ghost/Stealth/TLS/JA3Profile.cs
- Security review checklist: (attach completed checklist from above)
- Tests: (link to unit tests demonstrating canonical outputs)
- Owner: @team/stealth and security@example.com
- Acceptance criteria: (list the criteria from this document)

-- End of template --

## Change log

- 2026-03-01: Document created to provide audit rationale and governance checklist for MD5 use in JA3 fingerprinting.
