import { describe, expect, it } from 'vitest';
import { buildHallRequest, parseRowRange, previewSeats, safePreviewSeats } from './hallBuilder';

describe('parseRowRange', () => {
  it('expands letter and number ranges', () => {
    expect(parseRowRange('A-L')).toEqual(['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L']);
    expect(parseRowRange(' a – c ')).toEqual(['A', 'B', 'C']);
    expect(parseRowRange('1-12')).toHaveLength(12);
    expect(parseRowRange('1-12')![11]).toBe('12');
    expect(parseRowRange('A')).toEqual(['A']);
  });

  it('rejects reversed or mixed ranges', () => {
    expect(parseRowRange('L-A')).toBeNull();
    expect(parseRowRange('A-3')).toBeNull();
    expect(parseRowRange('')).toBeNull();
  });
});

describe('buildHallRequest', () => {
  it('counts seats and builds the API request', () => {
    const { request, seatCount, errors } = buildHallRequest('Büyük Salon', [
      { name: 'Salon', rows: 'A-L', seatsPerRow: 20 },
      { name: 'Balkon', rows: 'A-F', seatsPerRow: 18 },
    ]);

    expect(errors).toEqual([]);
    expect(seatCount).toBe(348);
    expect(request.sections[0]).toEqual({ name: 'Salon', rows: expect.arrayContaining([{ label: 'A', seatCount: 20 }]) });
    expect(request.sections[1]!.rows).toHaveLength(6);
  });

  it('rejects more than 5000 seats', () => {
    const { errors } = buildHallRequest('Dev', [{ name: 'Salon', rows: '1-60', seatsPerRow: 100 }]);

    expect(errors).toContain('Salon 1 ile 5.000 arasında koltuk içermeli.');
  });

  it('rejects duplicate section names and a missing hall name', () => {
    const { errors } = buildHallRequest(' ', [
      { name: 'Salon', rows: 'A', seatsPerRow: 5 },
      { name: 'salon ', rows: 'B', seatsPerRow: 5 },
    ]);

    expect(errors).toEqual(expect.arrayContaining(['Salon adı girin (en fazla 100 karakter).', 'Bölüm adları birbirinden farklı olmalı.']));
  });
});

describe('previewSeats', () => {
  it('turns the request into available seats for the map preview', () => {
    const seats = previewSeats({ name: 'X', sections: [{ name: 'Salon', rows: [{ label: 'A', seatCount: 3 }] }] });

    expect(seats.map((s) => [s.section, s.row, s.number, s.status])).toEqual([
      ['Salon', 'A', 1, 'Available'],
      ['Salon', 'A', 2, 'Available'],
      ['Salon', 'A', 3, 'Available'],
    ]);
  });
});

describe('safePreviewSeats', () => {
  it('builds nothing for a hall over the limit, so a typo cannot freeze the page', () => {
    const { request, seatCount } = buildHallRequest('Dev', [{ name: 'Salon', rows: 'A-Z', seatsPerRow: 200000 }]);

    expect(safePreviewSeats(request, seatCount)).toEqual([]);
  });

  it('builds the preview within the limit', () => {
    const { request, seatCount } = buildHallRequest('X', [{ name: 'Salon', rows: 'A-B', seatsPerRow: 3 }]);

    expect(safePreviewSeats(request, seatCount)).toHaveLength(6);
  });
});
