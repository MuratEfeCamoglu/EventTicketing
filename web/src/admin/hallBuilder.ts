import type { SeatDto } from '../api/types';

export type SectionDraft = { name: string; rows: string; seatsPerRow: number };
export type CreateHallRequest = { name: string; sections: { name: string; rows: { label: string; seatCount: number }[] }[] };

/** API limits (CreateHall validator). */
const HALL_NAME_MAX = 100;
const SECTION_NAME_MAX = 50;
const MIN_SEATS = 1;
const MAX_SEATS = 5000;

const LETTER = /^[A-Z]$/;
const NUMBER = /^\d{1,3}$/;

/** "A-L" / "A–L" → A…L, "1-12" → 1…12, "C" → C; reversed or mixed ranges are invalid (null). */
export function parseRowRange(text: string): string[] | null {
  const parts = text.trim().toUpperCase().split(/\s*[-–]\s*/);
  if (parts.length === 1) {
    const only = parts[0]!;
    return LETTER.test(only) || NUMBER.test(only) ? [only] : null;
  }
  if (parts.length !== 2) return null;
  const [from, to] = parts as [string, string];
  if (LETTER.test(from) && LETTER.test(to)) {
    const start = from.charCodeAt(0);
    const end = to.charCodeAt(0);
    return end < start ? null : Array.from({ length: end - start + 1 }, (_, i) => String.fromCharCode(start + i));
  }
  if (NUMBER.test(from) && NUMBER.test(to)) {
    const start = Number(from);
    const end = Number(to);
    return end < start ? null : Array.from({ length: end - start + 1 }, (_, i) => String(start + i));
  }
  return null;
}

/** Turns the builder rows into the POST /venues/{id}/halls body; errors are Turkish and mirror the API rules. */
export function buildHallRequest(name: string, sections: SectionDraft[]): { request: CreateHallRequest; seatCount: number; errors: string[] } {
  const errors: string[] = [];
  const hallName = name.trim();
  if (!hallName || hallName.length > HALL_NAME_MAX) errors.push('Salon adı girin (en fazla 100 karakter).');
  if (sections.length === 0) errors.push('En az bir bölüm ekleyin.');

  const seen = new Set<string>();
  let seatCount = 0;
  const built = sections.map((section, index) => {
    const sectionName = section.name.trim();
    const title = sectionName || `${index + 1}. bölüm`;
    if (!sectionName || sectionName.length > SECTION_NAME_MAX) errors.push(`${index + 1}. bölüm için ad girin (en fazla 50 karakter).`);
    const key = sectionName.toLocaleLowerCase('tr');
    if (sectionName && seen.has(key)) errors.push('Bölüm adları birbirinden farklı olmalı.');
    seen.add(key);

    const labels = parseRowRange(section.rows);
    if (!labels) errors.push(`"${title}" için geçerli bir sıra aralığı girin (ör. A-L ya da 1-12).`);
    const perRow = Math.floor(section.seatsPerRow);
    if (!Number.isFinite(perRow) || perRow < 1) errors.push(`"${title}" için sıra başına en az 1 koltuk girin.`);

    const rows = (labels ?? []).map((label) => ({ label, seatCount: Math.max(0, perRow || 0) }));
    seatCount += rows.reduce((sum, r) => sum + r.seatCount, 0);
    return { name: sectionName, rows };
  });

  if (seatCount < MIN_SEATS || seatCount > MAX_SEATS) errors.push('Salon 1 ile 5.000 arasında koltuk içermeli.');
  return { request: { name: hallName, sections: built }, seatCount, errors: [...new Set(errors)] };
}

/** Seats for the read-only map preview of a hall that does not exist yet. */
export function previewSeats(request: CreateHallRequest): SeatDto[] {
  return request.sections.flatMap((section) =>
    section.rows.flatMap((row) =>
      Array.from({ length: row.seatCount }, (_, i) => ({
        eventSeatId: `${section.name}|${row.label}|${i + 1}`,
        section: section.name,
        row: row.label,
        number: i + 1,
        price: 0,
        status: 'Available' as const,
      })),
    ),
  );
}

/** The preview only for halls within the limit: a typo such as 200,000 seats per row must not allocate millions of seats. */
export function safePreviewSeats(request: CreateHallRequest, seatCount: number): SeatDto[] {
  return seatCount >= MIN_SEATS && seatCount <= MAX_SEATS ? previewSeats(request) : [];
}
