# ADR-0007: Anti-Detection with Timezone/Locale Spoofing

## Status
Accepted (2026-01-28)

## Context
Platforms detect automation through browser fingerprints, timezone inconsistencies, and lack of human-like behavior.

## Decision
Implement comprehensive anti-detection:
- Timezone spoofing to match proxy location
- Locale and language matching
- Human interaction patterns (random delays, mouse movements)
- User agent rotation
- TLS fingerprint randomization

## Alternatives Considered
1. Stealth plugins only - Rejected: not comprehensive enough
2. Residential proxies only - Rejected: expensive, still detectable
3. No anti-detection - Rejected: immediate blocking

## Consequences
- Positive: Lower detection rates
- Positive: Higher success rates
- Negative: More complex implementation
- Negative: Ethical considerations

## Evidence
- **Documents:**
  - docs/archive/2026/01/28/docs_plan/plan8-linkedin-platform-upgrade.md
- **Commits:**
  - 0cb2ed1 - feat: Implement timezone/locale spoofing, human interaction, LinkedIn enhancements
- **Implementation:**
  - src/Core/Ghost/Stealth/StealthScripts.cs
  - src/Core/Ghost/Extensions/HumanInteractionExtensions.cs
