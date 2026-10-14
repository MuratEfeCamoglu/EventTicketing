import type { SeatDto } from '../api/types';

export type LayoutRow = { label: string; left: SeatDto[]; right: SeatDto[] };
export type LayoutSection = { name: string; price: number; rows: LayoutRow[] };

const collator = new Intl.Collator('tr', { numeric: true, sensitivity: 'base' });

/** "Balkon C-7" — the label the API uses for tickets. */
export function seatLabel(seat: Pick<SeatDto, 'section' | 'row' | 'number'>): string {
  return `${seat.section} ${seat.row}-${seat.number}`;
}

/**
 * Cinema-like layout from the flat seat list (design §6.3). The API keeps no physical positions, so: sections in the
 * hall's order (unknown ones after, alphabetically), rows numeric-aware ("1, 2, 10"; "A, AA, B"), seats by number, and
 * every row split in the middle by an aisle with the extra seat of an odd row on the left.
 */
export function buildSeatLayout(seats: SeatDto[], sectionOrder: string[]): LayoutSection[] {
  const bySection = new Map<string, Map<string, SeatDto[]>>();
  for (const seat of seats) {
    const rows = bySection.get(seat.section) ?? new Map<string, SeatDto[]>();
    const row = rows.get(seat.row) ?? [];
    row.push(seat);
    rows.set(seat.row, row);
    bySection.set(seat.section, rows);
  }

  const known = sectionOrder.filter((name) => bySection.has(name));
  const unknown = [...bySection.keys()].filter((name) => !sectionOrder.includes(name)).sort(collator.compare);

  return [...known, ...unknown].map((name) => {
    const rows = bySection.get(name)!;
    const ordered = [...rows.keys()].sort(collator.compare).map((label) => {
      const rowSeats = [...rows.get(label)!].sort((a, b) => a.number - b.number);
      const half = Math.ceil(rowSeats.length / 2);
      return { label, left: rowSeats.slice(0, half), right: rowSeats.slice(half) };
    });
    const price = ordered[0]?.left[0]?.price ?? 0;
    return { name, price, rows: ordered };
  });
}

/** Structure of a layout without seat states: changes only when the hall's sections, rows or seat counts change. */
export function layoutShape(layout: LayoutSection[]): string {
  return layout.map((s) => `${s.name}:${s.rows.map((r) => `${r.label}=${r.left.length + r.right.length}`).join(',')}`).join('|');
}
