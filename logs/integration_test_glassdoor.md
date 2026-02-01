# Glassdoor Integration Test

Endpoint: http://localhost:5000/api/jobs/search

Test run: $(date --iso-8601=seconds)

| # | Query | Location | HTTP | Success | Response Time (s) | Job Count | CSRF-Header-Count | Notes |
|---:|---|---|---:|---:|---:|---:|---:|---|
| 1 | software engineer | San Francisco | 000 | false | 0.011 | invalid_json | 0 |  |
| 2 | software engineer | New York | 000 | false | 0.008 | invalid_json | 0 |  |
| 3 | software engineer | Remote | 000 | false | 0.009 | invalid_json | 0 |  |
| 4 | software engineer | London | 000 | false | 0.008 | invalid_json | 0 |  |
| 5 | software engineer | Madrid | 000 | false | 0.008 | invalid_json | 0 |  |
| 6 | product manager | San Francisco | 000 | false | 0.008 | invalid_json | 0 |  |
| 7 | product manager | New York | 000 | false | 0.008 | invalid_json | 0 |  |
| 8 | product manager | Remote | 000 | false | 0.008 | invalid_json | 0 |  |
| 9 | product manager | London | 000 | false | 0.008 | invalid_json | 0 |  |
| 10 | product manager | Madrid | 000 | false | 0.008 | invalid_json | 0 |  |
| 11 | data scientist | San Francisco | 000 | false | 0.008 | invalid_json | 0 |  |
| 12 | data scientist | New York | 000 | false | 0.008 | invalid_json | 0 |  |
| 13 | data scientist | Remote | 000 | false | 0.008 | invalid_json | 0 |  |
| 14 | data scientist | London | 000 | false | 0.008 | invalid_json | 0 |  |
| 15 | data scientist | Madrid | 000 | false | 0.008 | invalid_json | 0 |  |
| 16 | nurse | San Francisco | 000 | false | 0.008 | invalid_json | 0 |  |
| 17 | nurse | New York | 000 | false | 0.016 | invalid_json | 0 |  |
| 18 | nurse | Remote | 000 | false | 0.009 | invalid_json | 0 |  |
| 19 | nurse | London | 000 | false | 0.008 | invalid_json | 0 |  |
| 20 | nurse | Madrid | 000 | false | 0.008 | invalid_json | 0 |  |

Test completed. Interpretation notes:
- Success = HTTP 2xx
- Job Count heuristically determined from JSON response
- CSRF-Header-Count = number of response headers that matched common CSRF header names (case-insensitive)

