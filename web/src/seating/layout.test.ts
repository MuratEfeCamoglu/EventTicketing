import { describe, expect, it } from 'vitest';
import type { SeatDto } from '../api/types';
import { buildSeatLayout, layoutShape, seatLabel } from './layout';

let next = 0;
function seat(section: string, row: string, number: number, price = 450, status: SeatDto['status'] = 'Available'): SeatDto {
  next += 1;
  return { eventSeatId: `s${next}`, section, row, number, price, status };
}

describe('buildSeatLayout', () => {
  it('orders sections by the given section order', () => {
    const layout = buildSeatLayout([seat('Balkon', 'A', 1), seat('Salon', 'A', 1, 750)], ['Salon', 'Balkon']);

    expect(layout.map((s) => s.name)).toEqual(['Salon', 'Balkon']);
    expect(layout[0]!.price).toBe(750);
  });

  it('appends sections missing from the order alphabetically', () => {
    const layout = buildSeatLayout([seat('Loca', 'A', 1), seat('Ek', 'A', 1), seat('Salon', 'A', 1)], ['Salon']);

    expect(layout.map((s) => s.name)).toEqual(['Salon', 'Ek', 'Loca']);
  });

  it('orders rows numeric-aware and seats by number', () => {
    const layout = buildSeatLayout(
      [seat('S', '10', 1), seat('S', '2', 1), seat('S', '1', 3), seat('S', '1', 1), seat('S', '1', 2)],
      ['S'],
    );
    expect(layout[0]!.rows.map((r) => r.label)).toEqual(['1', '2', '10']);
    const first = layout[0]!.rows[0]!;
    expect([...first.left, ...first.right].map((s) => s.number)).toEqual([1, 2, 3]);

    const letters = buildSeatLayout([seat('S', 'B', 1), seat('S', 'AA', 1), seat('S', 'A', 1)], ['S']);
    expect(letters[0]!.rows.map((r) => r.label)).toEqual(['A', 'AA', 'B']);
  });

  it('splits a row at the middle with the extra seat on the left', () => {
    const seven = buildSeatLayout(Array.from({ length: 7 }, (_, i) => seat('S', 'A', i + 1)), ['S'])[0]!.rows[0]!;
    expect([seven.left.length, seven.right.length]).toEqual([4, 3]);

    const eight = buildSeatLayout(Array.from({ length: 8 }, (_, i) => seat('S', 'A', i + 1)), ['S'])[0]!.rows[0]!;
    expect([eight.left.length, eight.right.length]).toEqual([4, 4]);
  });

  it('labels a seat with section, row and number', () => {
    expect(seatLabel(seat('Balkon', 'C', 7))).toBe('Balkon C-7');
  });
});

describe('layoutShape', () => {
  it('ignores seat status changes, so polling does not re-fit or re-centre the map', () => {
    const free = [seat('S', 'A', 1), seat('S', 'A', 2)];
    const taken = free.map((s, i) => (i === 0 ? { ...s, status: 'Held' as const } : s));

    expect(layoutShape(buildSeatLayout(taken, ['S']))).toBe(layoutShape(buildSeatLayout(free, ['S'])));
    expect(layoutShape(buildSeatLayout([...free, seat('S', 'B', 1)], ['S']))).not.toBe(layoutShape(buildSeatLayout(free, ['S'])));
  });
});
