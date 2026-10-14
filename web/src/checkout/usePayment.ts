import { useCallback, useRef, useState } from 'react';
import { api } from '../api/client';
import { ApiError } from '../api/errors';
import type { PaymentResult } from '../api/types';

export type PaymentToken = 'tok_success' | 'tok_declined';

/**
 * Pays a reservation with one Idempotency-Key per payment attempt (design §6.4): the key is reused while the outcome
 * is unknown (network failure, double click), so the API charges at most once; a definite answer from the API ends
 * the attempt and the next payment gets a new key.
 */
export function usePayment(reservationId: string) {
  const [pending, setPending] = useState(false);
  const key = useRef<{ reservationId: string; value: string } | null>(null);
  const inFlight = useRef<Promise<PaymentResult> | null>(null);

  const pay = useCallback(
    (paymentToken: PaymentToken): Promise<PaymentResult> => {
      if (inFlight.current) return inFlight.current;
      if (key.current?.reservationId !== reservationId) key.current = { reservationId, value: crypto.randomUUID() };
      const idempotencyKey = key.current.value;

      setPending(true);
      const attempt = api<PaymentResult>(`/reservations/${reservationId}/payments`, { method: 'POST', json: { paymentToken }, idempotencyKey })
        .then((result) => {
          key.current = null;
          return result;
        })
        .catch((error: unknown) => {
          // The API answered (declined, expired, …): this attempt is over. A network error keeps the key for the retry.
          if (error instanceof ApiError) key.current = null;
          throw error;
        })
        .finally(() => {
          inFlight.current = null;
          setPending(false);
        });
      inFlight.current = attempt;
      return attempt;
    },
    [reservationId],
  );

  return { pay, pending };
}
