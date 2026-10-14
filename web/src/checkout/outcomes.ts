import { ApiError, messageFor } from '../api/errors';
import type { ReservationStatus } from '../api/types';

export type PaymentOutcome = { kind: 'declined' } | { kind: 'expired' } | { kind: 'message'; message: string };

/** What the reservation page shows for a failed payment: a result screen, or the Turkish message (never nothing). */
export function paymentOutcome(error: ApiError): PaymentOutcome {
  if (error.code === 'payment.declined') return { kind: 'declined' };
  if (error.code === 'reservation.expired') return { kind: 'expired' };
  return { kind: 'message', message: messageFor(error) };
}

/** How long the server may lag the browser's countdown before the page shows the expired state. */
export const EXPIRY_POLL_MS = 2000;

/**
 * Re-read the reservation while the local countdown has ended but the server still reports Pending, e.g. when the
 * browser clock is a little ahead; otherwise no polling.
 */
export function reservationRefetchInterval(status: ReservationStatus | undefined, expiredLocally: boolean): number | false {
  return status === 'Pending' && expiredLocally ? EXPIRY_POLL_MS : false;
}
