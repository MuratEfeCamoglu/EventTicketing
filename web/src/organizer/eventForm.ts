import type { EventCategory, EventDetail, HallDetail } from '../api/types';

export type EventFormValues = {
  title: string;
  description: string;
  category: EventCategory;
  venueId: string;
  hallId: string;
  /** datetime-local value, "2026-11-20T20:00", in the browser's time zone. */
  startsAt: string;
  endsAt: string;
  /** Price text per section name, as typed ("450,50" or "450.50"). */
  prices: Record<string, string>;
};

export type EventRequest = {
  hallId: string;
  title: string;
  description: string;
  category: EventCategory;
  startsAt: string;
  endsAt: string;
  sectionPrices: { section: string; price: number }[];
};

export const emptyEventForm: EventFormValues = { title: '', description: '', category: 'Concert', venueId: '', hallId: '', startsAt: '', endsAt: '', prices: {} };

/** API limits (EventFieldsValidator). */
const TITLE_MAX = 200;
const DESCRIPTION_MAX = 4000;
const MAX_PRICE = 100_000;

export function parsePrice(text: string | undefined): number {
  return text ? Number(text.trim().replace(/\s/g, '').replace(',', '.')) : Number.NaN;
}

const pad = (n: number) => String(n).padStart(2, '0');

/** ISO instant → "YYYY-MM-DDTHH:mm" in local time, for a datetime-local input. */
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function toEventRequest(values: EventFormValues, sections: string[]): EventRequest {
  return {
    hallId: values.hallId,
    title: values.title.trim(),
    description: values.description.trim(),
    category: values.category,
    startsAt: new Date(values.startsAt).toISOString(),
    endsAt: new Date(values.endsAt).toISOString(),
    sectionPrices: sections.map((section) => ({ section, price: parsePrice(values.prices[section]) })),
  };
}

export function fromEvent(event: EventDetail, hall: HallDetail): EventFormValues {
  const prices = Object.fromEntries(event.sectionPrices.map((p) => [p.section, String(p.price)]));
  return {
    title: event.title,
    description: event.description,
    category: event.category,
    venueId: hall.venueId,
    hallId: hall.id,
    startsAt: toLocalInput(event.startsAt),
    endsAt: toLocalInput(event.endsAt),
    prices: Object.fromEntries(hall.sections.map((s) => [s.name, prices[s.name] ?? ''])),
  };
}

/** Field → Turkish message; price errors are keyed "price:<section>". Mirrors the API rules so they rarely reach it. */
export function validateEventForm(values: EventFormValues, sections: string[]): Record<string, string> {
  const errors: Record<string, string> = {};
  const title = values.title.trim();
  if (!title || title.length > TITLE_MAX) errors.title = 'Başlık girin (en fazla 200 karakter).';
  if (values.description.length > DESCRIPTION_MAX) errors.description = 'Açıklama en fazla 4.000 karakter olabilir.';
  if (!values.hallId) errors.hallId = 'Bir salon seçin.';
  if (!values.startsAt) errors.startsAt = 'Başlangıç zamanını girin.';
  if (!values.endsAt) errors.endsAt = 'Bitiş zamanını girin.';
  else if (values.startsAt && new Date(values.endsAt) <= new Date(values.startsAt)) errors.endsAt = 'Bitiş, başlangıçtan sonra olmalı.';
  for (const section of sections) {
    const price = parsePrice(values.prices[section]);
    if (!Number.isFinite(price) || price <= 0) errors[`price:${section}`] = 'Bu bölüm için 0’dan büyük bir fiyat girin.';
    else if (price > MAX_PRICE) errors[`price:${section}`] = 'Fiyat en fazla 100.000 ₺ olabilir.';
    else if (Math.round(price * 100) !== price * 100) errors[`price:${section}`] = 'Fiyatta en fazla 2 ondalık olabilir.';
  }
  return errors;
}
