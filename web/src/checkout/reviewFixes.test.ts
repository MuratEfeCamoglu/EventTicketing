import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/errors';
import { paymentOutcome, reservationRefetchInterval } from './outcomes';

describe('paymentOutcome', () => {
  it('maps declined and expired to their screens', () => {
    expect(paymentOutcome(new ApiError({ status: 402, code: 'payment.declined' }))).toEqual({ kind: 'declined' });
    expect(paymentOutcome(new ApiError({ status: 409, code: 'reservation.expired' }))).toEqual({ kind: 'expired' });
  });

  it('reports every other API failure with its Turkish message instead of staying silent', () => {
    expect(paymentOutcome(new ApiError({ status: 403, code: 'auth.forbidden' }))).toEqual({ kind: 'message', message: 'Bu işlem için yetkiniz yok.' });
    expect(paymentOutcome(new ApiError({ status: 500, code: 'server.error', traceId: 't9' }))).toEqual({ kind: 'message', message: 'Beklenmeyen bir hata oluştu (iz: t9).' });
  });
});

describe('reservationRefetchInterval', () => {
  it('keeps re-reading while the local countdown says expired but the server still says Pending', () => {
    expect(reservationRefetchInterval('Pending', true)).toBe(2000);
    expect(reservationRefetchInterval('Pending', false)).toBe(false);
    expect(reservationRefetchInterval('Expired', true)).toBe(false);
    expect(reservationRefetchInterval(undefined, true)).toBe(false);
  });
});
