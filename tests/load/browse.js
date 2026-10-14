// AC-20.4 / NFR-02: 50 virtual users browse for 60 s (GET /events, then GET /events/{id}).
// Run against the API started as in README ("Yük testi"): k6 run tests/load/browse.js
import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080/api/v1';

export const options = {
  scenarios: {
    browse: { executor: 'constant-vus', vus: 50, duration: '60s' },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    // NFR-02
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<200'],
  },
};

export function setup() {
  const response = http.get(`${BASE_URL}/events?pageSize=100`);
  const ids = response.json('items').map((e) => e.id);
  if (ids.length === 0) {
    throw new Error('No published events; start the API in Development so the sample data is seeded.');
  }
  return { ids };
}

export default function (data) {
  const list = http.get(`${BASE_URL}/events`, { tags: { name: 'GET /events' } });
  check(list, { 'list 200': (r) => r.status === 200 });

  const id = data.ids[Math.floor(Math.random() * data.ids.length)];
  const detail = http.get(`${BASE_URL}/events/${id}`, { tags: { name: 'GET /events/{id}' } });
  check(detail, { 'detail 200': (r) => r.status === 200 });
}
