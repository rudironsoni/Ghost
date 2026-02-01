# Google Jobs integration test learnings

- Test executed: 2026-02-01T08:13:41+01:00
- Result: Server not running locally; tests skipped

Findings:

- Local development server at http://localhost:5000/api/jobs/search did not respond within short connectivity check (connect-timeout 2s, max-time 5s).
- logs/integration_test_google.md created with documented skip.

Next steps:

1. Start the web API (dotnet run or host) locally and re-run the integration test.
2. Ensure health endpoint /api/jobs/health returns 200 before running the 20 requests.
