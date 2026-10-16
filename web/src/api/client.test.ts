import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeJwt } from '../test/jwt';
import { getTokens, setTokens } from '../auth/session';
import { api, apiBlob } from './client';
import { ApiError } from './errors';

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const accessToken = (n: number) => fakeJwt({ sub: 'u1', email: 'a@example.com', role: 'Customer', exp: 2_000_000_000 + n });

describe('api', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    setTokens({ accessToken: accessToken(1), refreshToken: 'r1' });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  const calls = (path: string) => fetchMock.mock.calls.filter(([url]) => String(url).endsWith(path));

  it('retries once with a refreshed token after 401', async () => {
    fetchMock
      .mockResolvedValueOnce(json(401, { status: 401, code: 'auth.unauthorized' }))
      .mockResolvedValueOnce(json(200, { accessToken: accessToken(2), accessTokenExpiresAt: '2033-01-01T00:00:00Z', refreshToken: 'r2' }))
      .mockResolvedValueOnce(json(200, { ok: true }));

    const result = await api<{ ok: boolean }>('/me/tickets');

    expect(result).toEqual({ ok: true });
    expect(calls('/api/v1/auth/refresh')).toHaveLength(1);
    const retried = new Headers(fetchMock.mock.calls[2]![1]!.headers);
    expect(retried.get('Authorization')).toBe(`Bearer ${accessToken(2)}`);
    expect(getTokens()?.refreshToken).toBe('r2');
  });

  it('clears the session and rejects when refresh fails', async () => {
    fetchMock
      .mockResolvedValueOnce(json(401, { status: 401, code: 'auth.unauthorized' }))
      .mockResolvedValueOnce(json(401, { status: 401, code: 'auth.refresh_token_expired' }));

    await expect(api('/me/tickets')).rejects.toMatchObject({ status: 401 });
    expect(getTokens()).toBeNull();
  });

  it('concurrent 401s share one refresh', async () => {
    fetchMock.mockImplementation(async (url, init) => {
      const path = String(url);
      if (path.endsWith('/auth/refresh')) {
        await new Promise((resolve) => setTimeout(resolve, 10));
        return json(200, { accessToken: accessToken(3), accessTokenExpiresAt: '2033-01-01T00:00:00Z', refreshToken: 'r3' });
      }
      const auth = new Headers(init?.headers).get('Authorization');
      return auth === `Bearer ${accessToken(3)}` ? json(200, { path }) : json(401, { status: 401, code: 'auth.unauthorized' });
    });

    await Promise.all([api('/me/tickets'), api('/me/reservations')]);

    expect(calls('/api/v1/auth/refresh')).toHaveLength(1);
  });

  it('asks the ngrok tunnel to skip its browser warning page on every call', async () => {
    fetchMock.mockResolvedValueOnce(json(200, {})).mockResolvedValueOnce(new Response('png'));

    await api('/events', { anonymous: true });
    await apiBlob('/tickets/t1/qr');

    for (const call of fetchMock.mock.calls) {
      expect(new Headers(call[1]!.headers).get('ngrok-skip-browser-warning')).toBe('true');
    }
  });

  it('sends Idempotency-Key when given', async () => {
    fetchMock.mockResolvedValueOnce(json(200, {}));

    await api('/reservations/r/payments', { method: 'POST', json: { paymentToken: 'tok_success' }, idempotencyKey: 'key-1' });

    const headers = new Headers(fetchMock.mock.calls[0]![1]!.headers);
    expect(headers.get('Idempotency-Key')).toBe('key-1');
    expect(headers.get('Content-Type')).toBe('application/json');
  });

  it('turns ProblemDetails into ApiError with the Retry-After value', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ status: 429, code: 'rate_limit.exceeded' }), { status: 429, headers: { 'Retry-After': '17' } }));

    const error = await api('/auth/login', { method: 'POST', json: {} }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).retryAfterSeconds).toBe(17);
  });
});
