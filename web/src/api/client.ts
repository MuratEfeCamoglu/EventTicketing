import { clearTokens, getTokens, setTokens, type Tokens } from '../auth/session';
import { ApiError } from './errors';

export const API_PREFIX = '/api/v1';

/**
 * Sent on every call: behind the ngrok tunnel (design W-10) the free tier answers browser requests with a warning page
 * instead of the API unless this header is present. It has no effect anywhere else.
 */
const TUNNEL_HEADERS = { 'ngrok-skip-browser-warning': 'true' } as const;

export type ApiInit = Omit<RequestInit, 'body'> & { json?: unknown; idempotencyKey?: string; anonymous?: boolean };

type TokenResponse = { accessToken: string; accessTokenExpiresAt: string; refreshToken: string };

let refreshInFlight: Promise<boolean> | null = null;

/**
 * One refresh at a time: concurrent 401s wait for the same POST /auth/refresh (design §8). Resolves false when the API
 * rejects the refresh token (the session is over); rejects when the API cannot be reached or fails with 5xx, so the
 * session survives an offline moment and the caller sees that error instead of a logout.
 */
function refreshTokens(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    const tokens = getTokens();
    if (!tokens) return false;
    const response = await fetch(`${API_PREFIX}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...TUNNEL_HEADERS },
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    });
    if (response.status >= 500) throw await ApiError.fromResponse(response);
    if (!response.ok) {
      clearTokens();
      return false;
    }
    const next = (await response.json()) as TokenResponse;
    setTokens({ accessToken: next.accessToken, refreshToken: next.refreshToken });
    return true;
  })().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

function buildRequest(init: ApiInit, tokens: Tokens | null): RequestInit {
  const { json, idempotencyKey, anonymous, headers, ...rest } = init;
  const merged = new Headers(headers);
  merged.set('Accept', 'application/json');
  for (const [name, value] of Object.entries(TUNNEL_HEADERS)) merged.set(name, value);
  if (json !== undefined) merged.set('Content-Type', 'application/json');
  if (idempotencyKey) merged.set('Idempotency-Key', idempotencyKey);
  if (tokens && !anonymous) merged.set('Authorization', `Bearer ${tokens.accessToken}`);
  return { ...rest, headers: merged, body: json === undefined ? undefined : JSON.stringify(json) };
}

async function send(path: string, init: ApiInit): Promise<Response> {
  const url = `${API_PREFIX}${path}`;
  const tokens = getTokens();
  const response = await fetch(url, buildRequest(init, tokens));
  if (response.status !== 401 || !tokens || init.anonymous) return response;

  // Access token expired or revoked: refresh once and repeat the request with the new token.
  if (!(await refreshTokens())) return response;
  return fetch(url, buildRequest(init, getTokens()));
}

/** JSON call to the API under /api/v1; failures become ApiError, 204 resolves to undefined. */
export async function api<T>(path: string, init: ApiInit = {}): Promise<T> {
  const response = await send(path, init);
  if (!response.ok) {
    if (response.status === 401) clearTokens();
    throw await ApiError.fromResponse(response);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

/** Binary call (ticket QR PNG) with the same auth and refresh rules. */
export async function apiBlob(path: string): Promise<Blob> {
  const response = await send(path, {});
  if (!response.ok) throw await ApiError.fromResponse(response);
  return response.blob();
}
