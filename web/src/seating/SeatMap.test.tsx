import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { SeatDto } from '../api/types';
import { SeatMap } from './SeatMap';

const seats: SeatDto[] = [
  { eventSeatId: 'a', section: 'Balkon', row: 'C', number: 6, price: 450, status: 'Available' },
  { eventSeatId: 'b', section: 'Balkon', row: 'C', number: 7, price: 450, status: 'Sold' },
  { eventSeatId: 'c', section: 'Balkon', row: 'C', number: 8, price: 450, status: 'Held' },
];

describe('SeatMap', () => {
  it('held and sold seats are not selectable', () => {
    render(<SeatMap seats={seats} sectionOrder={['Balkon']} selectedIds={new Set()} onToggle={() => {}} />);

    expect(screen.getByRole('button', { name: /Balkon C-7, 450,00.*₺, satıldı/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Balkon C-8, 450,00.*₺, başkası tuttu/ })).toBeDisabled();
  });

  it('seat buttons are keyboard focusable with an accessible name', async () => {
    const onToggle = vi.fn();
    render(<SeatMap seats={seats} sectionOrder={['Balkon']} selectedIds={new Set()} onToggle={onToggle} />);
    const free = screen.getByRole('button', { name: /Balkon C-6, 450,00.*₺, boş/ });

    free.focus();
    await userEvent.keyboard('{Enter}');

    expect(free).toHaveFocus();
    expect(onToggle).toHaveBeenCalledWith(seats[0]);
  });

  it('marks selected seats as pressed', () => {
    render(<SeatMap seats={seats} sectionOrder={['Balkon']} selectedIds={new Set(['a'])} onToggle={() => {}} />);

    expect(screen.getByRole('button', { name: /Balkon C-6.*seçili/ })).toHaveAttribute('aria-pressed', 'true');
  });

  it('read-only maps have no clickable seats', () => {
    render(<SeatMap seats={seats} sectionOrder={['Balkon']} selectedIds={new Set()} readOnly />);

    expect(screen.queryAllByRole('button', { name: /Balkon C-6/ }).every((b) => (b as HTMLButtonElement).disabled)).toBe(true);
  });

  it('renders a 5000-seat hall inside its own scrolling viewport', () => {
    const big: SeatDto[] = [];
    for (let r = 0; r < 50; r++) {
      for (let n = 1; n <= 100; n++) big.push({ eventSeatId: `${r}-${n}`, section: 'Salon', row: String(r + 1), number: n, price: 750, status: 'Available' });
    }

    render(<SeatMap seats={big} sectionOrder={['Salon']} selectedIds={new Set()} onToggle={() => {}} />);

    expect(screen.getAllByRole('button')).toHaveLength(5000 + 2); // seats + zoom in/out
    expect(screen.getByTestId('seat-map-viewport')).toHaveClass('overflow-auto');
  }, 60_000);
});
