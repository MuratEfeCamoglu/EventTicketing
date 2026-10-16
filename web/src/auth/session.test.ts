import { describe, expect, it, vi } from 'vitest';
import { fakeJwt } from '../test/jwt';
import { decodeSession, subscribe } from './session';

describe('decodeSession', () => {
  it('decodes sub, email, role and exp', () => {
    const token = fakeJwt({ sub: '0b6c2f1e-1111-4222-8333-944455556666', email: 'ayşe@example.com', role: 'Organizer', exp: 1_800_000_000 });

    expect(decodeSession(token)).toEqual({
      userId: '0b6c2f1e-1111-4222-8333-944455556666',
      email: 'ayşe@example.com',
      role: 'Organizer',
      expiresAt: new Date(1_800_000_000 * 1000),
    });
  });
});

describe('subscribe', () => {
  it('hears a login or logout made in another tab', () => {
    const listener = vi.fn();
    const unsubscribe = subscribe(listener);

    window.dispatchEvent(new StorageEvent('storage', { key: 'et.session' }));
    window.dispatchEvent(new StorageEvent('storage', { key: 'something-else' }));
    unsubscribe();
    window.dispatchEvent(new StorageEvent('storage', { key: 'et.session' }));

    expect(listener).toHaveBeenCalledTimes(1);
  });
});
