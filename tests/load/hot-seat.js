// AC-20.4: 200 virtual users try to hold the same seat once each; exactly one may get 201.
// setup() registers and logs in 200 customers, so the API must run with RateLimiting__Enabled=false (README "Yük testi").
// Run: k6 run tests/load/hot-seat.js   (repeat with the API started with Locking__Enabled=false to compare)
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080/api/v1';
const USERS = 200;
const JSON_HEADERS = { 'Content-Type': 'application/json' };

const created = new Counter('holds_created');
const refused = new Counter('holds_refused');

export const options = {
  setupTimeout: '180s',
  scenarios: {
    hotSeat: { executor: 'per-vu-iterations', vus: USERS, iterations: 1, maxDuration: '60s' },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    holds_created: ['count==1'],
    holds_refused: [`count==${USERS - 1}`],
    // NFR-02 for POST /events/{id}/reservations
    'http_req_duration{name:POST /events/{id}/reservations}': ['p(95)<400'],
  },
};

export function setup() {
  const run = Date.now();
  const registrations = [];
  for (let i = 0; i < USERS; i++) {
    const email = `load-${run}-${i}@example.com`;
    registrations.push(['POST', `${BASE_URL}/auth/register`, JSON.stringify({ email, password: 'Passw0rd-123', fullName: `Load ${i}` }), { headers: JSON_HEADERS }]);
  }
  http.batch(registrations).forEach((r) => {
    if (r.status !== 201) throw new Error(`register failed: ${r.status} ${r.body}`);
  });

  const logins = registrations.map(([, , body]) => {
    const { email, password } = JSON.parse(body);
    return ['POST', `${BASE_URL}/auth/login`, JSON.stringify({ email, password }), { headers: JSON_HEADERS }];
  });
  const tokens = http.batch(logins).map((r) => {
    if (r.status !== 200) throw new Error(`login failed: ${r.status} ${r.body}`);
    return r.json('accessToken');
  });

  const eventId = http.get(`${BASE_URL}/events`).json('items')[0].id;
  const seat = http.get(`${BASE_URL}/events/${eventId}/seats`).json().find((s) => s.status === 'Available');
  if (!seat) throw new Error('No available seat left in the sample event; reset the database.');

  return { tokens, eventId, seatId: seat.eventSeatId };
}

export default function (data) {
  const response = http.post(
    `${BASE_URL}/events/${data.eventId}/reservations`,
    JSON.stringify({ eventSeatIds: [data.seatId] }),
    { headers: { ...JSON_HEADERS, Authorization: `Bearer ${data.tokens[__VU - 1]}` }, tags: { name: 'POST /events/{id}/reservations' } },
  );

  if (response.status === 201) created.add(1);
  else if (response.status === 409) refused.add(1);
  check(response, { '201 or 409': (r) => r.status === 201 || r.status === 409 });
}
