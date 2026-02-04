# RUNBOOK — Ghost Job Scraper Reliability

Operational guide for diagnosing and stabilizing job scraping across platforms (Indeed, Glassdoor, Google Jobs) with **multi-strategy parsing**, **Polly circuit breakers**, and **monitoring/health endpoints**.

## Quick triage (5 minutes)

1. **Is it platform-specific?**
   ```bash
   curl -s http://localhost:5000/health/platforms | jq
   ```
2. **Is a circuit breaker open?**
   ```bash
   curl -s http://localhost:5000/circuit-breakers | jq
   ```
3. **If results are empty:** enable DebugMode and reproduce once.

---

## 1) Common Issues and Troubleshooting

### A. Parsing returns empty results

**Symptoms**
- HTTP request succeeds, but returned `jobs` is empty.

**Likely causes**
- HTML/JSON structure changed (common for Google/Glassdoor).
- Anti-bot interstitial (consent, captcha, “unusual traffic”).
- Wrong strategy for current failure mode (e.g., HttpOnly hitting a consent page).

**Actions**
1. Enable platform debug output:
   ```json
   {
     "Ghost": {
       "Extensions": {
         "Google": { "DebugMode": true },
         "Glassdoor": { "DebugMode": true }
       }
     }
   }
   ```
   Expect artifacts under `logs/` (examples in repo docs: `logs/google_jobs_search.html`, `logs/glassdoor_search_*.json`).

2. Force a more resilient strategy (temporary):
   - Consent/captcha: **BrowserFirst** / **BrowserOnly**.
   - API-friendly: **HttpFirst**.
   ```bash
   # env var example
   export GHOST__EXTENSIONS__GOOGLE__STRATEGY=BrowserOnly
   export GHOST__EXTENSIONS__GOOGLE__DEBUGMODE=true
   ```

3. Inspect debug output:
   - Consent/captcha/blocked page → anti-bot/session/proxy issue.
   - Valid listings but 0 parsed → selectors/paths drifted (code fix).

4. Google Jobs (HTTP) quick check: cookies + status.
   ```bash
   curl -I -H "Cookie: CONSENT=YES+; SOCS=CAESE" \
     "https://www.google.com/search?q=software+engineer&tbm=jobs" \
     --max-time 10
   ```
   If 403/redirect/captcha: switch to BrowserFirst/BrowserOnly; review proxy/IP reputation.

**Escalate when**
- Debug output shows a stable page but selectors clearly no longer match (structural change) → requires code update to entity selectors/JSON paths.

---

### B. Circuit breaker keeps opening

**Symptoms**
- Requests are rejected quickly.
- `/circuit-breakers` shows `Open` or rapid oscillation `HalfOpen → Open`.

**Likely causes**
- Real upstream failures (403/429/5xx/timeouts).
- Treating anti-bot signals as failures (by design for sensitive platforms).
- Parser failures being counted as failures.

**Actions**
1. Confirm which platform breaker is opening:
   ```bash
   curl -s http://localhost:5000/circuit-breakers | jq '.[] | {platform: .platform, state: .state, failures: .failureCount, rejections: .rejectionCount}'
   ```

2. Check configuration (architecture doc indicates per-platform settings):
   ```json
   {
     "CircuitBreaker": {
       "Platforms": {
         "Glassdoor": { "FailureThreshold": 3, "OpenDurationSeconds": 60, "TreatAntiBotAsFailure": true },
         "Google":    { "FailureThreshold": 4, "OpenDurationSeconds": 45, "TreatAntiBotAsFailure": true },
         "Indeed":    { "FailureThreshold": 5, "OpenDurationSeconds": 30, "TreatAntiBotAsFailure": false }
       }
     }
   }
   ```

3. Determine dominant failure mode from logs:
   - 403/429/captcha: slow down, rotate proxy/session, prefer browser.
   - timeouts/network: reduce concurrency, increase timeout, check proxies.
   - parse: treat as a release blocker for that platform.

**Escalate when**
- Breaker opens on healthy upstream responses → bug in failure classification.

---

### C. High error rates from a specific platform

**Symptoms**
- `/health/platforms` shows one platform **Degraded/Unhealthy** while others are healthy.
- Metrics show high failures and/or latency for that platform.

**Actions**
1. Compare per-platform health and recent success timestamps:
   ```bash
   curl -s http://localhost:5000/health/platforms | jq
   curl -s http://localhost:5000/api/jobs/health | jq
   ```
2. Validate platform is enabled and strategy is appropriate:
   ```bash
   printenv | grep -E '^GHOST__EXTENSIONS__' | sort
   ```
3. Mitigate impact:
   - Reduce concurrency/backoff.
   - Force BrowserOnly if HTTP is returning interstitials.
   - Disable the platform if it is causing cascading failures.

---

### D. Memory/performance issues

**Symptoms**
- Increased latency, timeouts, or OOM kills.
- Browser pool saturation; frequent cold starts.

**Actions**
1. Reduce load first: lower concurrency and browser pool sizes.
2. Disable DebugMode in production (I/O + large artifacts).
3. If memory is tight, cut hot pool (browser instances are expensive; see architecture doc).

---

## 2) Diagnostic Procedures

### A. Check which parsing strategy was used

Parsers use a **three-tier fallback**:
1) DotnetSpider entity parsing → 2) JSON parsing → 3) Regex heuristics.

**Procedure**
1. Enable DebugMode.
2. Re-run a single request.
3. Look for strategy attempt/success/failure logs (e.g., `StrategyAttempt`).

**Interpretation**
- Strategy 1 failing across the board: selectors likely stale.
- Strategy 2 failing: JSON format changed or classification is wrong.
- Falling to strategy 3 frequently: you are in “best effort” mode; expect reduced data quality.

---

### B. View circuit breaker state

**Procedure**
```bash
curl -s http://localhost:5000/circuit-breakers | jq
```

**Interpretation**
- `Closed`: normal.
- `Open`: requests blocked until `OpenDurationSeconds` expires.
- `HalfOpen`: probing; a single failure may re-open.

---

### C. Check platform health metrics

**Procedure**
```bash
curl -s http://localhost:5000/health/platforms | jq
curl -s http://localhost:5000/metrics | head -n 50
```

---

### D. Interpret logs

Look for:
- **Correlation IDs** across request → parsing → metrics.
- Error categories: `Auth`, `Network`, `Parse`, `RateLimit`.
- Anti-bot signals (403/429, CAPTCHA, consent pages).

Minimal grep patterns:
```bash
grep -i "google.*jobs\|glassdoor\|indeed" logs/*.log | tail -200
grep -i "circuit\|halfopen\|open" logs/*.log | tail -200
grep -i "parse\|strategy" logs/*.log | tail -200
```

---

## 3) Emergency Procedures

### A. Manually reset a circuit breaker

Preferred: use the service endpoint (if implemented):
```bash
curl -X POST http://localhost:5000/circuit-breakers/Glassdoor/reset
```

Fallback (no reset endpoint): restart the service to clear in-memory policy state.

**Caution**: resetting without addressing the root cause can cause immediate re-open under load.

---

### B. Force a specific parsing strategy

Use platform Strategy to steer execution:
```bash
export GHOST__EXTENSIONS__GLASSDOOR__STRATEGY=BrowserFirst
export GHOST__EXTENSIONS__GLASSDOOR__TIMEOUT=45000
```

If you have an internal feature flag for parser selection, use it only temporarily and record it in incident notes.

---

### C. Disable a problematic platform

```bash
export GHOST__EXTENSIONS__GOOGLE__ENABLED=false
export GHOST__EXTENSIONS__GLASSDOOR__ENABLED=false
```
Restart service after changing environment variables.

---

### D. Clear metrics and start fresh

Preferred: expose a metrics reset endpoint (if available):
```bash
curl -X POST http://localhost:5000/metrics/reset
```

Fallback: restart service (in-memory retention resets). If metrics are persisted externally, clear at the store per your ops policy.

---

## 4) Performance Tuning

### A. Adjust circuit breaker thresholds

Use per-platform tuning (sensitive platforms should trip faster to prevent bans):
```json
{
  "CircuitBreaker": {
    "Platforms": {
      "Google": { "FailureThreshold": 6, "OpenDurationSeconds": 60 },
      "Glassdoor": { "FailureThreshold": 3, "OpenDurationSeconds": 120 }
    }
  }
}
```

Guidance:
- If bans/403s spike: **lower** threshold and **increase** open duration.
- If transient timeouts spike: modestly **increase** threshold but fix timeouts/concurrency first.

---

### B. Tune parser timeout values

```bash
export GHOST__EXTENSIONS__GOOGLE__TIMEOUT=45000
export GHOST__EXTENSIONS__GLASSDOOR__TIMEOUT=45000
```

Guidance: increase timeouts only when browser/proxy rendering is the bottleneck.

---

### C. Configure pool sizes

The architecture references hot/warm/cold pools; browser instances consume significant memory.

Guidance:
- Reduce hot pool if memory is tight; expect higher latency from cold starts.
- Increase warm pool if you need better tail latency without the full hot-pool footprint.

---

### D. Optimize for specific platforms

- **Indeed (official API)**: HttpFirst; higher concurrency is usually safer.
- **Google/Glassdoor (anti-bot)**: BrowserFirst/BrowserOnly; lower concurrency; stronger backoff; proxies.

---

## 5) Decision Trees

### 1. “Empty results” flow

1) Empty results → enable DebugMode → reproduce once
- Consent/captcha/403/429: BrowserOnly + reduce rate + rotate proxy/session
- Valid listings but 0 parsed: selectors/paths drifted → escalate

### 2. “Circuit breaker open” flow

1) Breaker Open → identify platform → inspect recent errors
- RateLimit/Anti-bot: slow down + BrowserOnly + increase open duration
- Network/Timeout: increase timeout + reduce concurrency + check proxies
- Parse dominates: disable platform until fixed

### 3. “One platform unhealthy” flow

1) Platform unhealthy → optionally disable to protect SLOs
2) Verify remaining platforms stable
3) DebugMode + single request
4) Decide: config tweak vs rollback vs dev escalation
