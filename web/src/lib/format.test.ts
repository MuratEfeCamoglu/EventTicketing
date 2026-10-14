import { describe, expect, it } from 'vitest';
import { categoryLabel, formatDateShort, formatDateTime, formatPrice } from './format';

const nbsp = new RegExp(String.fromCharCode(0xa0), "g");

describe('format', () => {
  it('formats prices as Turkish lira', () => {
    expect(formatPrice(450).replace(nbsp, ' ')).toBe('450,00 ₺');
    expect(formatPrice(1250.5).replace(nbsp, ' ')).toBe('1.250,50 ₺');
  });

  it('formats a date-time in Turkish in the given time zone', () => {
    expect(formatDateTime('2026-11-13T17:00:00Z', 'Europe/Istanbul')).toBe('13 Kasım 2026 Cuma, 20:00');
  });

  it('formats a short date', () => {
    expect(formatDateShort('2026-11-13T17:00:00Z', 'Europe/Istanbul')).toBe('13 Kas');
  });

  it('labels every API category in Turkish', () => {
    expect(['Concert', 'Theatre', 'Sports', 'Conference', 'Other'].map((c) => categoryLabel(c as never))).toEqual(['Konser', 'Tiyatro', 'Spor', 'Konferans', 'Diğer']);
  });
});
