import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { usePayment } from './usePayment';

const ok = () => new Response(JSON.stringify({ paymentId: 'p1', status: 'Succeeded', tickets: [] }), { status: 200, headers: { 'Content-Type': 'application/json' } });
const keyOf = (call: Parameters<typeof fetch>) => new Headers(call[1]?.headers).get('Idempotency-Key');

describe('usePayment', () => {
  const fetchMock = vi.fn<typeof fetch>();
  beforeEach(() => vi.stubGlobal('fetch', fetchMock));
  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('reuses the same Idempotency-Key when retried after a network error', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch')).mockResolvedValueOnce(ok());
    const { result } = renderHook(() => usePayment('r1'));

    await act(async () => {
      await result.current.pay('tok_success').catch(() => undefined);
    });
    await act(async () => {
      await result.current.pay('tok_success');
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(keyOf(fetchMock.mock.calls[0]!)).toBeTruthy();
    expect(keyOf(fetchMock.mock.calls[1]!)).toBe(keyOf(fetchMock.mock.calls[0]!));
  });

  it('uses a new key for a new reservation', async () => {
    fetchMock.mockImplementation(async () => ok());
    const { result, rerender } = renderHook(({ id }) => usePayment(id), { initialProps: { id: 'r1' } });
    await act(async () => void (await result.current.pay('tok_success')));

    rerender({ id: 'r2' });
    await act(async () => void (await result.current.pay('tok_success')));

    expect(keyOf(fetchMock.mock.calls[1]!)).not.toBe(keyOf(fetchMock.mock.calls[0]!));
  });

  it('ignores a second pay() while one is pending', async () => {
    let release: (r: Response) => void = () => {};
    fetchMock.mockImplementation(() => new Promise<Response>((resolve) => (release = resolve)));
    const { result } = renderHook(() => usePayment('r1'));

    let first: Promise<unknown> = Promise.resolve();
    let second: Promise<unknown> = Promise.resolve();
    act(() => {
      first = result.current.pay('tok_success');
      second = result.current.pay('tok_success');
    });
    release(ok());
    await act(async () => {
      await Promise.all([first, second]);
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
