import http from 'k6/http';
import { check, sleep } from 'k6';

// Peak scenario: 50,000 jobs/day = ~2,083 jobs/hour = ~35 jobs/minute
// Target: Validate production capacity at planned scale
export const options = {
  stages: [
    { duration: '2m', target: 50 },   // Ramp up to 50 VU quickly
    { duration: '58m', target: 50 },  // Sustained peak load
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<8000'],  // 95% under 8s
    http_req_failed: ['rate<0.05'],      // <5% errors
    http_reqs: ['rate>35'],              // >35 req/s
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  const platform = Math.random() > 0.5 ? 'linkedin' : 'indeed';
  const queries = ['software-engineer', 'data-scientist', 'product-manager', 'devops', 'frontend-developer'];
  const query = queries[Math.floor(Math.random() * queries.length)];
  const locations = ['remote', 'san-francisco', 'new-york', 'london', 'berlin'];
  const location = locations[Math.floor(Math.random() * locations.length)];

  const response = http.get(
    `${BASE_URL}/api/scrape/${platform}?query=${query}&location=${location}&limit=20`,
    { tags: { platform } }
  );

  check(response, {
    [`${platform} status is 200`]: (r) => r.status === 200,
    [`${platform} response time < 15s`]: (r) => r.timings.duration < 15000,
    [`${platform} has jobs`]: (r) => r.json('jobs') !== undefined,
  });

  sleep(0.5);  // ~120 requests per minute per VU = ~6,000/min = ~360,000/hour
}
