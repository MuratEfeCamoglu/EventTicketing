import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SeatDto } from '../api/types';
import { useSeatSelection } from './useSeatSelection';

const seat = (n: number): SeatDto => ({ eventSeatId: `s${n}`, section: 'Salon', row: 'A', number: n, price: 750, status: 'Available' });

describe('useSeatSelection', () => {
  it('toggles seats', () => {
    const { result } = renderHook(() => useSeatSelection());

    act(() => void result.current.toggle(seat(1)));
    act(() => void result.current.toggle(seat(2)));
    expect(result.current.selected.map((s) => s.eventSeatId)).toEqual(['s1', 's2']);

    let outcome = '';
    act(() => {
      outcome = result.current.toggle(seat(1));
    });
    expect(outcome).toBe('removed');
    expect(result.current.selected.map((s) => s.eventSeatId)).toEqual(['s2']);
  });

  it("returns 'limit' on the seventh seat and does not add it", () => {
    const { result } = renderHook(() => useSeatSelection(6));
    for (let n = 1; n <= 6; n++) act(() => void result.current.toggle(seat(n)));

    let outcome = '';
    act(() => {
      outcome = result.current.toggle(seat(7));
    });

    expect(outcome).toBe('limit');
    expect(result.current.selected).toHaveLength(6);
  });

  it('drops seats that became unavailable', () => {
    const { result } = renderHook(() => useSeatSelection());
    act(() => void result.current.toggle(seat(1)));
    act(() => void result.current.toggle(seat(2)));

    act(() => result.current.remove(['s1']));

    expect(result.current.selected.map((s) => s.eventSeatId)).toEqual(['s2']);
  });

  it('drops seats that are no longer available before counting the limit', () => {
    const { result } = renderHook(() => useSeatSelection(2));
    act(() => void result.current.toggle(seat(1)));
    act(() => void result.current.toggle(seat(2)));

    let outcome = '';
    act(() => {
      outcome = result.current.toggle(seat(3), new Set(['s2', 's3']));
    });

    expect(outcome).toBe('added');
    expect(result.current.selected.map((s) => s.eventSeatId)).toEqual(['s2', 's3']);
  });
});
