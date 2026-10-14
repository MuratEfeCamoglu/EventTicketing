/** Builds an unsigned JWT with the given payload (the client never checks signatures); UTF-8 safe like a real token. */
export function fakeJwt(payload: Record<string, unknown>): string {
  const encode = (value: object) => {
    const bytes = new TextEncoder().encode(JSON.stringify(value));
    return btoa(String.fromCharCode(...bytes)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  };
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`;
}
