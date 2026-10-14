import type { EventCategory } from '../api/types';

// Non-breaking space keeps the amount and the sign on one line.
const NBSP = String.fromCharCode(0xa0);

const amount = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 1250.5 → "1.250,50 ₺": Turkish grouping, the sign after the amount as on Turkish price tags (CLDR puts it first). */
export function formatPrice(value: number): string {
  return `${amount.format(value)}${NBSP}₺`;
}

/** "13 Kasım 2026 Cuma, 20:00" in the user's (or the given) time zone. */
export function formatDateTime(iso: string, timeZone?: string): string {
  const date = new Date(iso);
  const day = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', weekday: 'long', timeZone }).format(date);
  const time = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit', timeZone }).format(date);
  return `${day}, ${time}`;
}

/** "13 Kas" */
export function formatDateShort(iso: string, timeZone?: string): string {
  return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', timeZone }).format(new Date(iso));
}

/** "20:00" */
export function formatTime(iso: string, timeZone?: string): string {
  return new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit', timeZone }).format(new Date(iso));
}

const categories: Record<EventCategory, string> = { Concert: 'Konser', Theatre: 'Tiyatro', Sports: 'Spor', Conference: 'Konferans', Other: 'Diğer' };

export function categoryLabel(category: EventCategory): string {
  return categories[category];
}

export const allCategories = Object.keys(categories) as EventCategory[];
