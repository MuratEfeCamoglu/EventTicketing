import { useCallback, useEffect, useRef, useState } from 'react';
import type { SeatDto } from '../api/types';

/** The API limit of seats per reservation (ISKELET Reservations:MaxSeatsPerReservation). */
export const MAX_SEATS = 6;

export type ToggleOutcome = 'added' | 'removed' | 'limit';

const storageKey = (eventId: string) => `et.selection.${eventId}`;

/** Keeps the chosen seats across the login page, which a guest visits before holding them (sessionStorage: this tab). */
export function rememberSelection(eventId: string, seats: SeatDto[]): void {
  try {
    sessionStorage.setItem(storageKey(eventId), JSON.stringify(seats));
  } catch {
    // Storage unavailable (private mode): the guest picks the seats again after logging in.
  }
}

function remembered(eventId: string | undefined): SeatDto[] {
  if (!eventId) return [];
  try {
    const raw = sessionStorage.getItem(storageKey(eventId));
    return raw ? (JSON.parse(raw) as SeatDto[]) : [];
  } catch {
    return [];
  }
}

/** Selected seats; with an eventId, starts from the seats remembered for that event and then forgets them. */
export function useSeatSelection(max = MAX_SEATS, eventId?: string) {
  const [selected, setSelected] = useState<SeatDto[]>(() => remembered(eventId));
  // Mirror of the state, so toggle can report its outcome synchronously.
  const current = useRef<SeatDto[]>(selected);

  // Removed after the first render rather than while reading, so a StrictMode double render still restores them.
  useEffect(() => {
    if (!eventId) return;
    try {
      sessionStorage.removeItem(storageKey(eventId));
    } catch {
      // Storage unavailable: nothing was remembered.
    }
  }, [eventId]);

  const commit = useCallback((next: SeatDto[]) => {
    current.current = next;
    setSelected(next);
  }, []);

  const toggle = useCallback(
    (seat: SeatDto, available?: Set<string>): ToggleOutcome => {
      // Seats taken by others since they were chosen leave the selection, so they no longer count toward the limit.
      const list = available ? current.current.filter((s) => available.has(s.eventSeatId)) : current.current;
      if (list.some((s) => s.eventSeatId === seat.eventSeatId)) {
        commit(list.filter((s) => s.eventSeatId !== seat.eventSeatId));
        return 'removed';
      }
      if (list.length >= max) {
        if (list.length !== current.current.length) commit(list);
        return 'limit';
      }
      commit([...list, seat]);
      return 'added';
    },
    [commit, max],
  );

  const remove = useCallback((ids: string[]) => commit(current.current.filter((s) => !ids.includes(s.eventSeatId))), [commit]);
  const clear = useCallback(() => commit([]), [commit]);

  return { selected, toggle, remove, clear };
}
