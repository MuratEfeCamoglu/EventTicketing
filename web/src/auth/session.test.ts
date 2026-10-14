import { describe, expect, it } from 'vitest';
import { fakeJwt } from '../test/jwt';
import { decodeSession } from './session';

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
