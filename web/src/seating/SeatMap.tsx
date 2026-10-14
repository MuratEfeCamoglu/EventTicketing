import { Minus, Plus } from 'lucide-react';
import { memo, useEffect, useMemo, useRef, useState, type MouseEvent, type FocusEvent } from 'react';
import type { SeatDto } from '../api/types';
import { formatPrice } from '../lib/format';
import { cx } from '../ui/cx';
import { buildSeatLayout, layoutShape, seatLabel } from './layout';

type Props = {
  seats: SeatDto[];
  sectionOrder: string[];
  selectedIds: Set<string>;
  onToggle?: (seat: SeatDto) => void;
  /** Preview mode (admin hall builder): seats are shown but cannot be chosen. */
  readOnly?: boolean;
};

const ZOOM_MIN = 0.3;
const ZOOM_MAX = 1.6;
const ZOOM_STEP = 0.2;

type SeatState = 'free' | 'selected' | 'held' | 'sold';

const stateText: Record<SeatState, string> = { free: 'boş', selected: 'seçili', held: 'başkası tuttu', sold: 'satıldı' };

const stateClasses: Record<SeatState, string> = {
  free: 'bg-seat-free border-seat-free-line hover:border-brand hover:bg-brand-soft',
  selected: 'bg-brand border-brand',
  held: 'bg-seat-held border-seat-held-line',
  sold: 'bg-seat-sold border-seat-sold',
};

function stateOf(seat: SeatDto, selected: boolean): SeatState {
  if (seat.status === 'Sold') return 'sold';
  if (seat.status === 'Held') return 'held';
  return selected ? 'selected' : 'free';
}

const Seat = memo(function Seat({ seat, selected, readOnly, onToggle }: { seat: SeatDto; selected: boolean; readOnly?: boolean; onToggle?: (seat: SeatDto) => void }) {
  const state = stateOf(seat, selected);
  const label = seatLabel(seat);
  return (
    <button
      type="button"
      data-tip={readOnly ? label : `${label} · ${formatPrice(seat.price)}`}
      aria-label={`${label}, ${formatPrice(seat.price)}, ${stateText[state]}`}
      aria-pressed={state === 'selected'}
      disabled={readOnly || state === 'held' || state === 'sold'}
      onClick={() => onToggle?.(seat)}
      className={cx(
        'relative size-6 shrink-0 rounded-t-[7px] rounded-b-[3px] border-2 transition-colors disabled:cursor-not-allowed',
        'after:absolute after:inset-x-0.5 after:-bottom-1 after:h-1 after:rounded-b after:bg-current after:opacity-20',
        stateClasses[state],
        readOnly && state === 'free' && 'hover:border-seat-free-line hover:bg-seat-free',
      )}
    />
  );
});

export function SeatLegend() {
  const items: [SeatState, string][] = [
    ['free', 'Boş'],
    ['selected', 'Seçili'],
    ['held', 'Başkası tuttu'],
    ['sold', 'Satıldı'],
  ];
  return (
    <ul className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted">
      {items.map(([state, text]) => (
        <li key={state} className="flex items-center gap-1.5">
          <span className={cx('size-3.5 rounded-t-[4px] rounded-b-[2px] border-2', stateClasses[state])} aria-hidden />
          {text}
        </li>
      ))}
    </ul>
  );
}

/**
 * Cinema-style seat map (design §6.3): curved stage, sections with name and price, row letters on both sides, an aisle
 * in the middle of every row. Large halls scroll inside their own viewport and can be zoomed; one shared tooltip is
 * positioned by event delegation, so thousands of seats stay cheap.
 */
export function SeatMap({ seats, sectionOrder, selectedIds, onToggle, readOnly }: Props) {
  const layout = useMemo(() => buildSeatLayout(seats, sectionOrder), [seats, sectionOrder]);
  // Seat states change on every poll; only a different hall structure re-fits and re-centres the map.
  const shape = useMemo(() => layoutShape(layout), [layout]);
  const [zoom, setZoom] = useState(1);
  const userZoomed = useRef(false);
  const viewport = useRef<HTMLDivElement>(null);
  const content = useRef<HTMLDivElement>(null);
  const tip = useRef<HTMLDivElement>(null);

  // A hall wider than its box starts zoomed out to fit; once the user zooms, their choice stays.
  useEffect(() => {
    const box = viewport.current;
    const inner = content.current;
    if (!box || !inner || typeof ResizeObserver === 'undefined') return;
    let centred = false;
    const fit = () => {
      if (userZoomed.current) return;
      const current = Number(inner.style.zoom || 1);
      const natural = inner.scrollWidth / current;
      const next = natural > box.clientWidth ? Math.max(ZOOM_MIN, Math.floor((box.clientWidth / natural) * 20) / 20) : 1;
      setZoom(next);
      if (centred) return;
      centred = true;
      // Whatever still does not fit opens at its centre, where the stage is; later viewport resizes keep the position.
      requestAnimationFrame(() => {
        box.scrollLeft = Math.max(0, (box.scrollWidth - box.clientWidth) / 2);
      });
    };
    const observer = new ResizeObserver(fit);
    observer.observe(box);
    return () => observer.disconnect();
  }, [shape]);

  const changeZoom = (delta: number) => {
    userZoomed.current = true;
    setZoom((z) => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.round((z + delta) * 100) / 100)));
  };

  function showTip(target: EventTarget) {
    const el = target as HTMLElement;
    const text = el.dataset?.tip;
    const box = viewport.current;
    const bubble = tip.current;
    if (!text || !box || !bubble) return;
    const seatBox = el.getBoundingClientRect();
    const frame = box.getBoundingClientRect();
    bubble.textContent = text;
    bubble.style.left = `${seatBox.left - frame.left + box.scrollLeft + seatBox.width / 2}px`;
    bubble.style.top = `${seatBox.top - frame.top + box.scrollTop - 6}px`;
    bubble.hidden = false;
  }
  const hideTip = () => {
    if (tip.current) tip.current.hidden = true;
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        {readOnly ? <span className="text-xs text-muted">Salt okunur önizleme</span> : <SeatLegend />}
        <div className="flex items-center gap-1">
          <button type="button" aria-label="Uzaklaştır" disabled={zoom <= ZOOM_MIN + 0.01} onClick={() => changeZoom(-ZOOM_STEP)} className="grid size-8 place-items-center rounded-lg border border-line-strong bg-surface text-ink-soft hover:bg-page disabled:opacity-40">
            <Minus className="size-4" />
          </button>
          <span className="w-12 text-center text-xs tabular-nums text-muted">{Math.round(zoom * 100)}%</span>
          <button type="button" aria-label="Yakınlaştır" disabled={zoom >= ZOOM_MAX - 0.01} onClick={() => changeZoom(ZOOM_STEP)} className="grid size-8 place-items-center rounded-lg border border-line-strong bg-surface text-ink-soft hover:bg-page disabled:opacity-40">
            <Plus className="size-4" />
          </button>
        </div>
      </div>

      <div
        ref={viewport}
        data-testid="seat-map-viewport"
        className="relative max-h-[70vh] overflow-auto rounded-xl border border-line bg-surface"
        onMouseOver={(e: MouseEvent) => showTip(e.target)}
        onMouseOut={hideTip}
        onFocus={(e: FocusEvent) => showTip(e.target)}
        onBlur={hideTip}
        onScroll={hideTip}
      >
        <div ref={content} className="mx-auto w-max min-w-full px-6 pb-8 pt-6" style={{ zoom }}>
          <div className="mx-auto mb-8 max-w-xl">
            <div className="h-6 rounded-[50%/100%_100%_0_0] border-t-4 border-brand/40 shadow-[0_-8px_18px_-6px_rgb(37_99_235/0.35)]" />
            <p className="-mt-2 text-center text-[11px] font-semibold tracking-[0.3em] text-muted">SAHNE</p>
          </div>
          {layout.map((section) => (
            <section key={section.name} className="mb-6 last:mb-0" aria-label={`${section.name} bölümü`}>
              <h3 className="mb-2 text-center text-xs font-bold tracking-wide text-ink-soft">
                {section.name.toLocaleUpperCase('tr')}{' '}
                {readOnly ? (
                  <span className="font-medium text-muted">· {section.rows.reduce((n, r) => n + r.left.length + r.right.length, 0)} koltuk</span>
                ) : (
                  <span className="font-medium text-muted">· {formatPrice(section.price)}</span>
                )}
              </h3>
              <div className="flex flex-col items-center gap-1.5">
                {section.rows.map((row) => (
                  <div key={row.label} className="flex items-center gap-1">
                    <span className="w-7 text-right text-[11px] font-medium text-faint">{row.label}</span>
                    <div className="flex gap-1">
                      {row.left.map((seat) => (
                        <Seat key={seat.eventSeatId} seat={seat} selected={selectedIds.has(seat.eventSeatId)} readOnly={readOnly} onToggle={onToggle} />
                      ))}
                    </div>
                    <span className="w-5 shrink-0" aria-hidden />
                    <div className="flex gap-1">
                      {row.right.map((seat) => (
                        <Seat key={seat.eventSeatId} seat={seat} selected={selectedIds.has(seat.eventSeatId)} readOnly={readOnly} onToggle={onToggle} />
                      ))}
                    </div>
                    <span className="w-7 text-[11px] font-medium text-faint">{row.label}</span>
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
        <div ref={tip} hidden role="tooltip" className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-full whitespace-nowrap rounded-md bg-ink px-2 py-1 text-xs font-medium text-white shadow-pop" />
      </div>
    </div>
  );
}
