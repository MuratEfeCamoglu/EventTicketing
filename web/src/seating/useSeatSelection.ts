import { useCallback, useRef, useState } from 'react';
import type { SeatDto } from '../api/types';

/** The API limit of seats per reservation (ISKELET Reservations:MaxSeatsPerReservation). */
export const MAX_SEATS = 6;

export type ToggleOutcome = 'added' | 'removed' | 'limit';

export function useSeatSelection(max = MAX_SEATS) {
  const [selected, setSelected] = useState<SeatDto[]>([]);
  // Mirror of the state, so toggle can report its outcome synchronously.
  const current = useRef<SeatDto[]>([]);

  const commit = useCallback((next: SeatDto[]) => {
    current.current = next;
    setSelected(next);
  }, []);

  const toggle = useCallback(
    (seat: SeatDto): ToggleOutcome => {
      const list = current.current;
      if (list.some((s) => s.eventSeatId === seat.eventSeatId)) {
        commit(list.filter((s) => s.eventSeatId !== seat.eventSeatId));
        return 'removed';
      }
      if (list.length >= max) return 'limit';
      commit([...list, seat]);
      return 'added';
    },
    [commit, max],
  );

  const remove = useCallback((ids: string[]) => commit(current.current.filter((s) => !ids.includes(s.eventSeatId))), [commit]);
  const clear = useCallback(() => commit([]), [commit]);

  return { selected, toggle, remove, clear };
}
