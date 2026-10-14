import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import type { Paged, ReservationListItem } from '../api/types';
import { reservationStatus } from '../checkout/status';
import { formatDateTime, formatPrice } from '../lib/format';
import { Badge } from '../ui/Badge';
import { Button, buttonClasses } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { Modal } from '../ui/Modal';
import { Pagination } from '../ui/Pagination';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';

export function ReservationsPage() {
  const [page, setPage] = useState(1);
  const [cancelId, setCancelId] = useState<string>();
  const [busy, setBusy] = useState(false);
  const toast = useToast();
  const queryClient = useQueryClient();
  const list = useQuery({ queryKey: ['reservations', page], queryFn: () => api<Paged<ReservationListItem>>(`/me/reservations?page=${page}&pageSize=10`) });

  async function cancel() {
    if (!cancelId) return;
    setBusy(true);
    try {
      await api(`/reservations/${cancelId}`, { method: 'DELETE' });
      toast.success('Rezervasyon iptal edildi.');
      await queryClient.invalidateQueries({ queryKey: ['reservations'] });
      void queryClient.invalidateQueries({ queryKey: ['seats'] });
    } catch (error) {
      toast.error(messageFor(error));
    } finally {
      setBusy(false);
      setCancelId(undefined);
    }
  }

  return (
    <div>
      <PageHeader title="Rezervasyonlarım" subtitle="Bekleyen rezervasyonları 10 dakika içinde ödeyin; aksi halde koltuklar serbest kalır." />
      {list.isPending ? (
        <LoadingBlock />
      ) : list.isError ? (
        <EmptyState title="Rezervasyonlar yüklenemedi" description={messageFor(list.error)} />
      ) : list.data.items.length === 0 ? (
        <EmptyState icon={<ClipboardList className="size-10" />} title="Henüz rezervasyonunuz yok" action={<Link to="/" className={buttonClasses('primary')}>Etkinliklere göz at</Link>} />
      ) : (
        <>
          <div className="flex flex-col gap-3">
            {list.data.items.map((r) => {
              const status = reservationStatus[r.status];
              return (
                <Card key={r.reservationId} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <Link to={`/etkinlik/${r.eventId}`} className="font-semibold hover:text-brand">
                        {r.eventTitle}
                      </Link>
                      <Badge tone={status.tone}>{status.label}</Badge>
                    </div>
                    <p className="mt-1 text-sm text-muted">
                      {formatDateTime(r.eventStartsAt)} · {r.seatCount} koltuk · {formatPrice(r.totalAmount)}
                    </p>
                    {r.status === 'Pending' && <p className="mt-0.5 text-xs text-warning">Son ödeme: {formatDateTime(r.expiresAt)}</p>}
                  </div>
                  {r.status === 'Pending' ? (
                    <div className="flex shrink-0 gap-2">
                      <Link to={`/rezervasyon/${r.reservationId}`} className={buttonClasses('primary', 'sm')}>
                        Ödemeye devam et
                      </Link>
                      <Button variant="secondary" size="sm" onClick={() => setCancelId(r.reservationId)}>
                        İptal et
                      </Button>
                    </div>
                  ) : (
                    <Link to={`/rezervasyon/${r.reservationId}`} className={buttonClasses('ghost', 'sm', 'shrink-0')}>
                      Ayrıntı
                    </Link>
                  )}
                </Card>
              );
            })}
          </div>
          <Pagination page={list.data.page} totalPages={list.data.totalPages} onPage={setPage} />
        </>
      )}
      <Modal open={!!cancelId} title="Rezervasyon iptal edilsin mi?" confirmLabel="İptal et" tone="danger" busy={busy} onConfirm={() => void cancel()} onClose={() => setCancelId(undefined)}>
        Koltuklar hemen serbest bırakılır.
      </Modal>
    </div>
  );
}
