# LinkedIn Plugin Cutover and Rollback

## Scope
This document defines phased cutover controls for the LinkedIn plugin path while preserving existing API behavior.

## Current Runtime Shape
- Web API registers LinkedIn via compile-time plugin extension in `src/Ghost.WebApi/Program.cs`.
- The plugin delegates to the existing platform extension implementation.
- Existing endpoint contracts remain unchanged for:
  - `/api/linkedin/*`
  - `/api/jobs/search`

## Cutover Toggles
Configuration section: `Ghost:Plugins:LinkedIn`

- `UsePluginRuntime` (default `true`)
  - `true`: plugin registers keyed worker mapping and plugin runtime metadata/readiness services.
  - `false`: plugin still delegates core LinkedIn service graph through the legacy extension path.

- `RegisterReadinessServices` (default `true`)
  - Controls registration of readiness/capability services.

- `RegisterKeyedJobClient` (default `true`)
  - Controls keyed `IJobClient` registration for worker key `linkedin`.

## Rollout Steps
1. Keep `Ghost:Extensions:LinkedIn:Enabled=true`.
2. Run verification gates and parity tests before rollout.
3. Enable/disable plugin toggles only through configuration changes.

## Rollback Plan
If regressions are detected:
1. Set:
   - `Ghost:Plugins:LinkedIn:UsePluginRuntime=false`
   - `Ghost:Plugins:LinkedIn:RegisterKeyedJobClient=false` (if worker issue is linked to keyed mapping)
2. Redeploy service.
3. Re-run health checks and targeted LinkedIn endpoint checks.
4. If needed, revert commit introducing the plugin cutover and redeploy.

## Verification Checklist
- `/api/linkedin/jobs/search` responds as expected.
- `/api/jobs/search` path still resolves `IJobClient` flow.
- Worker keyed LinkedIn job client path resolves successfully.
