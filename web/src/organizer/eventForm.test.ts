import { describe, expect, it } from 'vitest';
import type { EventDetail, HallDetail } from '../api/types';
import { emptyEventForm, fromEvent, toEventRequest, toLocalInput, validateEventForm, type EventFormValues } from './eventForm';

const values: EventFormValues = {
  title: 'Hamlet',
  description: 'Shakespeare',
  category: 'Theatre',
  venueId: 'v1',
  hallId: 'h1',
  startsAt: '2026-11-20T20:00',
  endsAt: '2026-11-20T22:30',
  prices: { Salon: '750', Balkon: '450,5' },
};

describe('toEventRequest', () => {
  it('converts local datetime to an ISO instant', () => {
    const request = toEventRequest(values, ['Salon', 'Balkon']);

    expect(request.startsAt).toBe(new Date('2026-11-20T20:00').toISOString());
    expect(request.endsAt).toBe(new Date('2026-11-20T22:30').toISOString());
  });

  it('maps prices for every hall section, accepting a decimal comma', () => {
    expect(toEventRequest(values, ['Salon', 'Balkon']).sectionPrices).toEqual([
      { section: 'Salon', price: 750 },
      { section: 'Balkon', price: 450.5 },
    ]);
  });
});

describe('fromEvent', () => {
  it('prefills every field from the organizer event response', () => {
    const event: EventDetail = {
      id: 'e1',
      title: 'Hamlet',
      description: 'Shakespeare',
      category: 'Theatre',
      startsAt: new Date('2026-11-20T20:00').toISOString(),
      endsAt: new Date('2026-11-20T22:30').toISOString(),
      status: 'Draft',
      venue: { id: 'v1', name: 'Mekân', city: 'Ankara', address: 'Adres' },
      hall: { id: 'h1', name: 'Salon' },
      sectionPrices: [
        { section: 'Balkon', price: 450 },
        { section: 'Salon', price: 750 },
      ],
    };
    const hall: HallDetail = { id: 'h1', venueId: 'v1', name: 'Salon', sections: [{ name: 'Salon', rows: [] }, { name: 'Balkon', rows: [] }] };

    expect(fromEvent(event, hall)).toEqual({ ...values, prices: { Salon: '750', Balkon: '450' } });
  });

  it('formats a date for a datetime-local input in local time', () => {
    expect(toLocalInput(new Date('2026-01-05T09:07').toISOString())).toBe('2026-01-05T09:07');
  });
});

describe('validateEventForm', () => {
  it('accepts a complete form', () => {
    expect(validateEventForm(values, ['Salon', 'Balkon'])).toEqual({});
  });

  it('reports a missing section price for that section', () => {
    expect(validateEventForm({ ...values, prices: { Salon: '750' } }, ['Salon', 'Balkon'])).toEqual({ 'price:Balkon': 'Bu bölüm için 0’dan büyük bir fiyat girin.' });
  });

  it('accepts 19,99 and 1.250 as valid prices', () => {
    expect(validateEventForm({ ...values, prices: { Salon: '19,99', Balkon: '1.250' } }, ['Salon', 'Balkon'])).toEqual({});
  });

  it('rejects an end before the start, an empty title and a price over the limit', () => {
    const errors = validateEventForm({ ...emptyEventForm, hallId: 'h1', startsAt: '2026-11-20T20:00', endsAt: '2026-11-20T19:00', prices: { Salon: '100001' } }, ['Salon']);

    expect(errors).toMatchObject({
      title: 'Başlık girin (en fazla 200 karakter).',
      endsAt: 'Bitiş, başlangıçtan sonra olmalı.',
      'price:Salon': 'Fiyat en fazla 100.000 ₺ olabilir.',
    });
  });

  it('refuses to save while the hall sections are still unknown', () => {
    const errors = validateEventForm({ ...values, prices: {} }, undefined);

    expect(errors.hallId).toBe('Salonun bölümleri yüklenemedi; biraz bekleyip tekrar deneyin.');
  });
});
