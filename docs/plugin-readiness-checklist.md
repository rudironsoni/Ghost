# Plugin Readiness Checklist

Use this checklist before marking any platform plugin as ready.

## 1) ABI Compliance
- [ ] Plugin implements `IExtension` without breaking existing contract behavior.
- [ ] Plugin metadata (`Name`, `Version`, `ProvidedServices`, `RequiredServices`) is covered by tests.
- [ ] Plugin-specific options are configuration-bound and validated.

## 2) Capability Metadata
- [ ] Capabilities model exists and is registered in DI.
- [ ] Capabilities accurately describe runtime requirements (browser/proxy/features).
- [ ] Capabilities are validated by automated tests.

## 3) Offline Self-Test
- [ ] Deterministic mock-server fixture exists (WireMock or equivalent).
- [ ] Plugin tests execute without public internet dependency.
- [ ] Happy-path and error-path fixtures are both covered.

## 4) Timeout and Cancellation
- [ ] Tests include explicit timeout ceilings.
- [ ] Runtime paths that may block support `CancellationToken`.
- [ ] CI lanes include hang diagnostics artifacts for troubleshooting.

## 5) Observability and Operations
- [ ] Readiness/self-test endpoint or service exists for plugin health validation.
- [ ] Logging is structured and actionable for plugin registration/runtime failures.
- [ ] Rollback plan for plugin cutover is documented.

## 6) Lane Placement
- [ ] Unit plugin tests are tagged for PR-blocking unit lane.
- [ ] Mock-server integration tests are tagged `Category=Integration` + `Capability=RequiresMockServer`.
- [ ] Live provider tests are isolated from PR-blocking lanes.
