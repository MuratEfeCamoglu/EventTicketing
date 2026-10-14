import { describe, expect, it } from 'vitest';
import { ApiError, messageFor } from './errors';

describe('messageFor', () => {
  it('maps known codes to Turkish', () => {
    const error = new ApiError({ status: 409, code: 'seat.unavailable' });

    expect(messageFor(error)).toBe('Seçtiğiniz koltuklardan biri az önce başkası tarafından alındı.');
  });

  it('uses Retry-After for rate_limit.exceeded', () => {
    const error = new ApiError({ status: 429, code: 'rate_limit.exceeded', retryAfterSeconds: 42 });

    expect(messageFor(error)).toBe('Çok fazla deneme. 42 sn sonra tekrar deneyin.');
  });

  it('falls back with traceId for unknown codes', () => {
    const error = new ApiError({ status: 500, code: 'something.new', traceId: 'abc123' });

    expect(messageFor(error)).toBe('Beklenmeyen bir hata oluştu (iz: abc123).');
  });

  it('keeps validation field errors', async () => {
    const response = new Response(
      JSON.stringify({ status: 400, code: 'validation.failed', traceId: 't1', errors: { password: ['Password is too short.'] } }),
      { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
    );

    const error = await ApiError.fromResponse(response);

    expect(error.code).toBe('validation.failed');
    expect(error.fieldErrors).toEqual({ password: ['Password is too short.'] });
    expect(messageFor(error)).toBe('Lütfen işaretli alanları düzeltin.');
  });

  it('exposes ProblemDetails extensions such as unavailableSeatIds', async () => {
    const response = new Response(JSON.stringify({ status: 409, code: 'seat.unavailable', unavailableSeatIds: ['s1', 's2'] }), { status: 409 });

    const error = await ApiError.fromResponse(response);

    expect(error.extensions.unavailableSeatIds).toEqual(['s1', 's2']);
  });
});
