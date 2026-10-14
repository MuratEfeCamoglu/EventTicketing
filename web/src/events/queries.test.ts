import { describe, expect, it } from 'vitest';
import { eventsQueryString, filtersFromSearch } from './queries';

describe('eventsQueryString', () => {
  it('skips empty values and encodes the rest', () => {
    expect(eventsQueryString({ city: 'İstanbul', q: '', page: 2 })).toBe('?city=%C4%B0stanbul&page=2');
  });

  it('is empty when no filter is set', () => {
    expect(eventsQueryString({})).toBe('');
  });

  it('round-trips through the page URL', () => {
    const filters = { city: 'Ankara', category: 'Theatre', q: 'hamlet', sort: '-startsAt', page: 3 };

    expect(filtersFromSearch(new URLSearchParams(eventsQueryString(filters)))).toEqual(filters);
  });
});
