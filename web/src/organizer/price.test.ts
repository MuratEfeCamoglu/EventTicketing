import { describe, expect, it } from 'vitest';
import { formatPriceInput, parsePrice } from './eventForm';

describe('parsePrice', () => {
  it('accepts prices with two decimals that are not exact binary fractions', () => {
    expect(parsePrice('19,99')).toBe(19.99);
    expect(parsePrice('8,2')).toBe(8.2);
    expect(parsePrice('0,29')).toBe(0.29);
  });

  it('reads Turkish thousands separators instead of treating them as decimals', () => {
    expect(parsePrice('1.250')).toBe(1250);
    expect(parsePrice('1.500')).toBe(1500);
    expect(parsePrice('1.250,50')).toBe(1250.5);
    expect(parsePrice('12.345.678,9')).toBe(12345678.9);
  });

  it('still accepts a dot as the decimal separator for one or two decimals', () => {
    expect(parsePrice('450.5')).toBe(450.5);
    expect(parsePrice('19.99')).toBe(19.99);
  });

  it('rejects ambiguous or malformed input', () => {
    expect(parsePrice('1,234')).toBeNaN();
    expect(parsePrice('1.25.0')).toBeNaN();
    expect(parsePrice('abc')).toBeNaN();
    expect(parsePrice('')).toBeNaN();
  });
});

describe('formatPriceInput', () => {
  it('shows stored prices with a decimal comma', () => {
    expect(formatPriceInput(450.5)).toBe('450,5');
    expect(formatPriceInput(750)).toBe('750');
  });
});
