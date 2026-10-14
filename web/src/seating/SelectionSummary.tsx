import { Armchair, X } from 'lucide-react';
import type { ReactNode } from 'react';
import type { SeatDto } from '../api/types';
import { formatPrice } from '../lib/format';
import { Button } from '../ui/Button';
import { Card } from '../ui/Card';
import { seatLabel } from './layout';
import { MAX_SEATS } from './useSeatSelection';

type Props = {
  selected: SeatDto[];
  onRemove: (seat: SeatDto) => void;
  onHold: () => void;
  holding: boolean;
  /** Replaces the hold button, e.g. for organizers who cannot buy. */
  blocked?: ReactNode;
};

const total = (seats: SeatDto[]) => seats.reduce((sum, s) => sum + s.price, 0);

/** Desktop: a sticky side panel listing the chosen seats. */
export function SelectionPanel({ selected, onRemove, onHold, holding, blocked }: Props) {
  return (
    <Card className="sticky top-20 hidden flex-col gap-4 p-5 lg:flex">
      <div>
        <h2 className="font-semibold">Seçimleriniz</h2>
        <p className="text-xs text-muted">En fazla {MAX_SEATS} koltuk. Ayırdıktan sonra ödemek için 10 dakikanız olur.</p>
      </div>
      {selected.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-lg bg-page py-6 text-center text-sm text-muted">
          <Armchair className="size-6 text-faint" />
          Haritadan koltuk seçin
        </div>
      ) : (
        <ul className="flex flex-col divide-y divide-line">
          {selected.map((seat) => (
            <li key={seat.eventSeatId} className="flex items-center justify-between gap-2 py-2 text-sm">
              <span>{seatLabel(seat)}</span>
              <span className="flex items-center gap-2">
                <span className="font-medium">{formatPrice(seat.price)}</span>
                <button type="button" aria-label={`${seatLabel(seat)} koltuğunu çıkar`} onClick={() => onRemove(seat)} className="rounded p-0.5 text-faint hover:bg-page hover:text-danger">
                  <X className="size-4" />
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}
      <div className="flex items-center justify-between border-t border-line pt-3 font-semibold">
        <span>Toplam</span>
        <span>{formatPrice(total(selected))}</span>
      </div>
      {blocked ?? (
        <Button onClick={onHold} loading={holding} disabled={selected.length === 0}>
          Koltukları ayır
        </Button>
      )}
    </Card>
  );
}

/** Phone: a bar fixed to the bottom of the screen with the total and the hold button. */
export function SelectionBar({ selected, onHold, holding, blocked }: Props) {
  return (
    <div className="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 px-4 py-3 shadow-pop backdrop-blur lg:hidden">
      {blocked ?? (
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="font-bold">{formatPrice(total(selected))}</p>
            <p className="truncate text-xs text-muted">{selected.length === 0 ? 'Koltuk seçin' : selected.map(seatLabel).join(', ')}</p>
          </div>
          <Button onClick={onHold} loading={holding} disabled={selected.length === 0} className="shrink-0">
            Koltukları ayır
          </Button>
        </div>
      )}
    </div>
  );
}
