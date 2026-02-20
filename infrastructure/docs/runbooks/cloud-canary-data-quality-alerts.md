# Cloud Canary and Data Quality Alert Runbook

## Purpose

This runbook defines response steps for Cloud canary reliability and data quality SLO alerts.

## SLOs

1. Canary success ratio SLO: >= 99% over 30 minutes.
2. Canary duration SLO: p95 <= 120 seconds over 30 minutes.
3. Data quality completeness SLO: >= 95%.
4. Data quality duplicate ratio SLO: <= 3%.
5. Data quality volume ratio SLO: >= 70% of rolling baseline.

## Alert-to-Action Mapping

### CanarySloBurnRateFast (critical)

- Meaning: canary success ratio dropped below fast-burn threshold.
- Immediate actions:
  1. Check scheduler and canary dispatch health.
  2. Inspect latest failed run diagnostics links from run status.
  3. If provider-wide, reduce canary schedule frequency for impacted provider and open incident.

### CanarySloBurnRateSlow (warning)

- Meaning: prolonged degradation below steady-state SLO.
- Immediate actions:
  1. Compare affected providers and regions.
  2. Check recent deploys and configuration changes.
  3. Start mitigation issue for provider parser/anti-bot drift.

### CanaryDurationSloBreach (warning)

- Meaning: canary execution latency SLO exceeded.
- Immediate actions:
  1. Check queue depth and worker availability.
  2. Check downstream provider latency and throttling signals.
  3. Verify no scheduler backlog buildup.

### DataQualityCompletenessRegression (critical)

- Meaning: required-field completeness dropped below threshold.
- Immediate actions:
  1. Identify top failing fields and providers from diagnostics.
  2. Validate HTML/API schema drift.
  3. Apply parser hotfix or temporary provider downgrade policy.

### DataQualityDuplicateSpike (warning)

- Meaning: duplicate ratio exceeded normal operating limit.
- Immediate actions:
  1. Validate dedupe key generation and upstream IDs.
  2. Check replay/cassette normalization and pagination logic.
  3. Add temporary dedupe hardening if user-facing impact is high.

### DataQualityVolumeDrop (critical)

- Meaning: extracted record volume dropped versus baseline.
- Immediate actions:
  1. Correlate with blocked/rate-limited classifications.
  2. Check provider-specific outages and auth/token status.
  3. Escalate to provider owner if sustained > 1h.

## Triage Checklist

1. Confirm alert labels: `provider`, `tenant_id`, `classification`, `run_kind`.
2. Pull latest run IDs and diagnostics URIs for affected providers.
3. Verify whether issue is isolated (single provider) or systemic (scheduler/infra).
4. Create incident if critical alert persists for more than 15 minutes.
5. Link incident notes to the Linear task and affected canary runs.

## Exit Criteria

- Alert condition cleared for two consecutive evaluation windows.
- Root cause documented with corrective action.
- Any threshold tuning tracked as a follow-up issue.
