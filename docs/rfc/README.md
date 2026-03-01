# Request for Comments (RFCs)

Records of significant architectural proposals and design decisions in the Ghost platform.

## Index

- [Testing Architecture Overhaul - Deterministic Test Lanes](./testing-architecture-overhaul.md) - Comprehensive test architecture refactoring with deterministic lanes, mock platforms, and flake governance
- [Testing Lanes Implementation](./testing-lanes.md) - Implementation details for the three-lane CI architecture (PR blocking, merge gate, nightly/live)

## Format

Each RFC follows the standard format:
- **Status**: Draft/Accepted/Rejected/Superseded
- **Executive Summary**: Problem statement, goals, and scope
- **Taxonomy and Capability Model**: Test categories and capability traits
- **Test Topology**: Project structure and migration plan
- **Parallelization Matrix**: Lane definitions and resource isolation
- **Mock Platform Architecture**: WireMock.NET and synthetic server usage
- **Provider Contract Model**: Contract definitions and enforcement
- **Complex Scenario Coverage**: Consent, scroll, pagination, dedupe scenarios
- **CI Lane Governance**: PR gate, merge gate, live smoke policies
- **Flake Governance**: Budgets, quarantine, RCA requirements
- **Migration and Rollback Plan**: Phase-by-phase migration and rollback procedures

## Related Documentation

- [Test Tier Audit Report](../test-tier-audit.md) - Classification of all test files in the Ghost project
- [Flaky Test Governance Policy](../flaky-test-policy.md) - Policy for managing flaky tests
- [Agent Instructions](../../AGENTS.md) - Agent execution and verification policies
