import http from 'k6/http';
import { check, sleep } from 'k6';

// Baseline scenario: 1,000 jobs/day = ~42 jobs/hour = ~0.7 jobs/minute
// Target: Gentle baseline to validate system stability
export const options = {
  stages: [
    { duration: '5m', target: 5 },   // Ramp up to 5 VU
    { duration: '30m', target: 5 },  // Steady state for 30 minutes
    { duration: '5m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<5000'],  // 95% under 5s
    http_req_failed: ['rate<0.1'],       // <10% errors
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // Test LinkedIn scraping endpoint
  const linkedInResponse = http.get(`${BASE_URL}/api/scrape/linkedin?query=software-engineer&location=remote&limit=10`, {
    tags: { platform: 'linkedin' },
  });

  check(linkedInResponse, {
    'LinkedIn status is 200': (r) => r.status === 200,
    'LinkedIn response time < 10s': (r) => r.timings.duration < 10000,
    'LinkedIn has jobs': (r) => r.json('jobs') !== undefined,
  });

  sleep(10);  // ~6 requests per minute per VU

  // Test Indeed scraping endpoint
  const indeedResponse = http.get(`${BASE_URL}/api/scrape/indeed?query=software-engineer&location=remote&limit=10`, {
    tags: { platform: 'indeed' },
  });

  check(indeedResponse, {
    'Indeed status is 200': (r) => r.status === 200,
    'Indeed response time < 10s': (r) => r.timings.duration < 10000,
    'Indeed has jobs': (r) => r.json('jobs') !== undefined,
  });

  sleep(10);
}
