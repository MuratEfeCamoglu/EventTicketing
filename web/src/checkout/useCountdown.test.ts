import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useCountdown } from './useCountdown';

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-01T10:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('counts down from expiresAt', () => {
    const { result } = renderHook(() => useCountdown('2026-11-01T10:10:00Z'));
    expect(result.current.label).toBe('10:00');
    expect(result.current.expired).toBe(false);

    act(() => vi.advanceTimersByTime(1000));

    expect(result.current.label).toBe('09:59');
    expect(result.current.secondsLeft).toBe(599);
  });

  it('is expired at zero and never negative', () => {
    const { result } = renderHook(() => useCountdown('2026-11-01T10:00:02Z'));

    act(() => vi.advanceTimersByTime(5000));

    expect(result.current.secondsLeft).toBe(0);
    expect(result.current.label).toBe('00:00');
    expect(result.current.expired).toBe(true);
  });

  it('follows the server value, not the time the page was opened', () => {
    const { result, rerender } = renderHook(({ at }) => useCountdown(at), { initialProps: { at: '2026-11-01T10:05:00Z' } });
    expect(result.current.label).toBe('05:00');

    rerender({ at: '2026-11-01T10:02:00Z' });

    expect(result.current.label).toBe('02:00');
  });
});
