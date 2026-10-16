export type Role = 'Customer' | 'Organizer' | 'Admin';

export type Tokens = { accessToken: string; refreshToken: string };

export type Session = { userId: string; email: string; role: Role; expiresAt: Date };

const STORAGE_KEY = 'et.session';
const listeners = new Set<() => void>();

/** Reads the claims the API puts in the access token: sub, email, role, exp (design §6.6). */
export function decodeSession(accessToken: string): Session {
  const payload = accessToken.split('.')[1];
  if (!payload) throw new Error('Malformed access token');
  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '='));
  const claims = JSON.parse(new TextDecoder().decode(Uint8Array.from(binary, (c) => c.charCodeAt(0)))) as Record<string, unknown>;
  return {
    userId: String(claims.sub),
    email: String(claims.email),
    role: claims.role as Role,
    expiresAt: new Date(Number(claims.exp) * 1000),
  };
}

export function getTokens(): Tokens | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Tokens) : null;
  } catch {
    return null;
  }
}

export function setTokens(tokens: Tokens | null): void {
  try {
    if (tokens) localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage can be unavailable (private mode); the session then lives only until reload.
  }
  listeners.forEach((listener) => listener());
}

export function clearTokens(): void {
  setTokens(null);
}

/**
 * Notifies when tokens change (login, refresh, logout, expiry), including changes made in another tab, which reach
 * this one as a storage event. Returns an unsubscribe function.
 */
export function subscribe(listener: () => void): () => void {
  const onStorage = (event: StorageEvent) => {
    if (event.key === STORAGE_KEY || event.key === null) listener();
  };
  listeners.add(listener);
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('storage', onStorage);
  };
}

export function currentSession(): Session | null {
  const tokens = getTokens();
  if (!tokens) return null;
  try {
    return decodeSession(tokens.accessToken);
  } catch {
    return null;
  }
}
