import { useQuery } from '@tanstack/react-query';
import { Info } from 'lucide-react';
import { useMemo } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import type { HallDetail, SeatDto } from '../api/types';
import { useAuth } from '../auth/AuthProvider';
import { loginPath } from '../auth/RequireRole';
import { EmptyState } from '../ui/Card';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';
import { SeatMap } from './SeatMap';
import { SelectionBar, SelectionPanel } from './SelectionSummary';
import { useHoldSeats } from './useHoldSeats';
import { MAX_SEATS, useSeatSelection } from './useSeatSelection';

/** The map refreshes on its own so seats taken by others show up (design §6.3). */
const REFRESH_MS = 15_000;

export function SeatSelection({ eventId, hall }: { eventId: string; hall: HallDetail }) {
  const { session } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const seats = useQuery({
    queryKey: ['seats', eventId],
    queryFn: () => api<SeatDto[]>(`/events/${eventId}/seats`, { anonymous: true }),
    refetchInterval: REFRESH_MS,
  });
  const selection = useSeatSelection();
  const hold = useHoldSeats(eventId, selection.remove);
  const sectionOrder = useMemo(() => hall.sections.map((s) => s.name), [hall]);

  // Seats that stopped being available since they were chosen are not offered for holding.
  const available = useMemo(() => new Set((seats.data ?? []).filter((s) => s.status === 'Available').map((s) => s.eventSeatId)), [seats.data]);
  const chosen = selection.selected.filter((s) => available.has(s.eventSeatId));
  const chosenIds = useMemo(() => new Set(chosen.map((s) => s.eventSeatId)), [chosen]);

  function toggle(seat: SeatDto) {
    if (selection.toggle(seat) === 'limit') toast.error(`Bir rezervasyonda en fazla ${MAX_SEATS} koltuk seçebilirsiniz.`);
  }

  function holdSeats() {
    if (!session) {
      navigate(loginPath(location.pathname));
      return;
    }
    hold.mutate(chosen.map((s) => s.eventSeatId));
  }

  const blocked =
    session && session.role !== 'Customer' ? (
      <p className="flex items-start gap-2 rounded-lg bg-brand-soft px-3 py-2.5 text-sm text-brand">
        <Info className="mt-0.5 size-4 shrink-0" />
        <span>
          Bilet almak için müşteri hesabıyla girin.{' '}
          <Link to={loginPath(location.pathname)} className="font-semibold underline">
            Hesap değiştir
          </Link>
        </span>
      </p>
    ) : undefined;

  if (seats.isPending) return <LoadingBlock label="Koltuklar yükleniyor…" />;
  if (seats.isError) return <EmptyState title="Koltuklar yüklenemedi" description={messageFor(seats.error)} />;

  const summary = { selected: chosen, onRemove: (seat: SeatDto) => selection.toggle(seat), onHold: holdSeats, holding: hold.isPending, blocked };

  return (
    <div className="grid gap-6 pb-24 lg:grid-cols-[minmax(0,1fr)_300px] lg:pb-0">
      <div className="min-w-0">
        <h2 className="mb-3 text-lg font-semibold">Koltuğunuzu seçin</h2>
        <SeatMap seats={seats.data} sectionOrder={sectionOrder} selectedIds={chosenIds} onToggle={toggle} />
      </div>
      <SelectionPanel {...summary} />
      <SelectionBar {...summary} />
    </div>
  );
}
