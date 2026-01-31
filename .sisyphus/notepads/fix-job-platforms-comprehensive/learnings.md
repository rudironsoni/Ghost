Changes made to GoogleJobsBrowserClient to improve consent bypass and human-like behavior.

Summary:
- Added randomized delays (RandomDelayAsync) to emulate human timing between actions.
- Added SimulateGlobalMouseMovementAsync which dispatches synthetic mousemove events in-page to appear active.
- Added HumanLikeScrollAsync to perform gentle scrolling passes.
- Enhanced HandleConsentPageAsync with multiple strategies: explicit reject selectors, customize->confirm flow, scanning for negative text, JS-based click of negative buttons, setting consent cookie as a last resort.
- Added RetryAsync with exponential backoff around consent handling to increase robustness.

Notes:
- Kept existing behavior and selectors intact; additions are best-effort and non-destructive.
- EvaluateAsync calls forward CancellationToken to satisfy analyzers.
- No changes to public API surfaces.

Next steps / Observations:
- May tune delays and mouse movement patterns based on real-world runs.
- Consider integrating Playwright stealth plugins or more advanced JS fingerprint mitigation if needed.
