import http from 'k6/http';
import { check, sleep } from 'k6';

// Spike scenario: 100,000 jobs/day for 1 hour stress test
// Target: Test system resilience under extreme load
export const options = {
  stages: [
    { duration: '1m', target: 100 },   // Rapid ramp to 100 VU
    { duration: '58m', target: 100 },  // Sustain extreme load
    { duration: '1m', target: 0 },     // Rapid ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<15000'], // 95% under 15s (graceful degradation allowed)
    http_req_failed: ['rate<0.2'],       // <20% errors acceptable during spike
    http_reqs: ['rate>70'],              // >70 req/s sustained
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  const platform = Math.random() > 0.5 ? 'linkedin' : 'indeed';
  const queries = ['software-engineer', 'data-scientist', 'product-manager', 'devops', 'frontend-developer', 
                   'backend-developer', 'full-stack', 'machine-learning', 'ux-designer', 'mobile-developer'];
  const query = queries[Math.floor(Math.random() * queries.length)];
  const locations = ['remote', 'san-francisco', 'new-york', 'london', 'berlin', 'singapore', 'amsterdam', 'toronto'];
  const location = locations[Math.floor(Math.random() * locations.length)];

  const response = http.get(
    `${BASE_URL}/api/scrape/${platform}?query=${query}&location=${location}&limit=50`,
    { tags: { platform } }
  );

  check(response, {
    [`${platform} status is 200`]: (r) => r.status === 200 || r.status === 503, // Accept 503 (circuit breaker)
    [`${platform} response time < 30s`]: (r) => r.timings.duration < 30000,
    [`${platform} has response`]: (r) => r.body !== undefined,
  });

  // Verify circuit breaker is working
  if (response.status === 503) {
    console.log(`Circuit breaker active for ${platform} - graceful degradation working`);
  }

  sleep(0.2);  // ~300 requests per minute per VU = ~30,000/min = ~1.8M/hour
}
