import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, ExternalLink, Pencil } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import type { EventStatus, OrganizerEventItem, Paged } from '../api/types';
import { formatDateTime } from '../lib/format';
import { Badge, type Tone } from '../ui/Badge';
import { Button, buttonClasses } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { Modal } from '../ui/Modal';
import { Pagination } from '../ui/Pagination';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';

export const eventStatus: Record<EventStatus, { label: string; tone: Tone }> = {
  Draft: { label: 'Taslak', tone: 'neutral' },
  Published: { label: 'Yayında', tone: 'success' },
  Cancelled: { label: 'İptal', tone: 'danger' },
};

type Confirm = { kind: 'delete' | 'cancel'; event: OrganizerEventItem };

export function MyEventsPage() {
  const [page, setPage] = useState(1);
  const [confirm, setConfirm] = useState<Confirm>();
  const [busyId, setBusyId] = useState<string>();
  const toast = useToast();
  const queryClient = useQueryClient();
  const list = useQuery({ queryKey: ['organizer-events', page], queryFn: () => api<Paged<OrganizerEventItem>>(`/organizer/events?page=${page}&pageSize=20`) });

  async function run(id: string, action: () => Promise<unknown>, success: string) {
    setBusyId(id);
    try {
      await action();
      toast.success(success);
      await queryClient.invalidateQueries({ queryKey: ['organizer-events'] });
      void queryClient.invalidateQueries({ queryKey: ['events'] });
    } catch (error) {
      toast.error(messageFor(error));
    } finally {
      setBusyId(undefined);
      setConfirm(undefined);
    }
  }

  const publish = (e: OrganizerEventItem) => run(e.id, () => api(`/events/${e.id}/publish`, { method: 'POST' }), `"${e.title}" yayımlandı.`);
  const confirmAction = () => {
    if (!confirm) return;
    const { kind, event } = confirm;
    void (kind === 'delete'
      ? run(event.id, () => api(`/events/${event.id}`, { method: 'DELETE' }), `"${event.title}" silindi.`)
      : run(event.id, () => api(`/events/${event.id}/cancel`, { method: 'POST' }), `"${event.title}" iptal edildi.`));
  };

  function actions(e: OrganizerEventItem) {
    const busy = busyId === e.id;
    return (
      <div className="flex flex-wrap gap-1.5">
        {e.status !== 'Cancelled' && (
          <Link to={`/yonetim/etkinlikler/${e.id}/duzenle`} className={buttonClasses('secondary', 'sm')}>
            <Pencil className="size-3.5" /> Düzenle
          </Link>
        )}
        {e.status === 'Draft' && (
          <>
            <Button size="sm" loading={busy} onClick={() => void publish(e)}>
              Yayımla
            </Button>
            <Button size="sm" variant="dangerGhost" disabled={busy} onClick={() => setConfirm({ kind: 'delete', event: e })}>
              Sil
            </Button>
          </>
        )}
        {e.status === 'Published' && (
          <>
            <Link to={`/etkinlik/${e.id}`} className={buttonClasses('ghost', 'sm')}>
              <ExternalLink className="size-3.5" /> Görüntüle
            </Link>
            <Button size="sm" variant="dangerGhost" disabled={busy} onClick={() => setConfirm({ kind: 'cancel', event: e })}>
              İptal et
            </Button>
          </>
        )}
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="Etkinliklerim"
        subtitle="Taslak oluşturun, düzenleyin ve hazır olunca yayımlayın."
        actions={
          <Link to="/yonetim/etkinlikler/yeni" className={buttonClasses('primary')}>
            <CalendarPlus className="size-4" /> Yeni etkinlik
          </Link>
        }
      />
      {list.isPending ? (
        <LoadingBlock />
      ) : list.isError ? (
        <EmptyState title="Etkinlikler yüklenemedi" description={messageFor(list.error)} />
      ) : list.data.items.length === 0 ? (
        <EmptyState icon={<CalendarPlus className="size-10" />} title="Henüz etkinliğiniz yok" description="İlk etkinliğinizi taslak olarak oluşturun." />
      ) : (
        <>
          <Card className="hidden overflow-hidden md:block">
            <table className="w-full text-sm">
              <thead className="bg-page text-left text-xs font-semibold text-muted">
                <tr>
                  <th className="px-4 py-3">Etkinlik</th>
                  <th className="px-4 py-3">Tarih</th>
                  <th className="px-4 py-3">Durum</th>
                  <th className="px-4 py-3 text-right">İşlemler</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {list.data.items.map((e) => (
                  <tr key={e.id} className="align-middle">
                    <td className="px-4 py-3">
                      <p className="font-semibold text-ink">{e.title}</p>
                      <p className="text-xs text-muted">
                        {e.venueName} · {e.hallName} · {e.city}
                      </p>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-ink-soft">{formatDateTime(e.startsAt)}</td>
                    <td className="px-4 py-3">
                      <Badge tone={eventStatus[e.status].tone}>{eventStatus[e.status].label}</Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end">{actions(e)}</div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
          <div className="flex flex-col gap-3 md:hidden">
            {list.data.items.map((e) => (
              <Card key={e.id} className="flex flex-col gap-3 p-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-semibold">{e.title}</p>
                    <p className="text-xs text-muted">{formatDateTime(e.startsAt)}</p>
                    <p className="text-xs text-muted">
                      {e.venueName} · {e.hallName}
                    </p>
                  </div>
                  <Badge tone={eventStatus[e.status].tone}>{eventStatus[e.status].label}</Badge>
                </div>
                {actions(e)}
              </Card>
            ))}
          </div>
          <Pagination page={list.data.page} totalPages={list.data.totalPages} onPage={setPage} />
        </>
      )}
      <Modal
        open={!!confirm}
        title={confirm?.kind === 'delete' ? 'Taslak silinsin mi?' : 'Etkinlik iptal edilsin mi?'}
        confirmLabel={confirm?.kind === 'delete' ? 'Sil' : 'İptal et'}
        tone="danger"
        busy={!!busyId}
        onConfirm={confirmAction}
        onClose={() => setConfirm(undefined)}
      >
        {confirm?.kind === 'delete' ? `"${confirm.event.title}" kalıcı olarak silinecek.` : `"${confirm?.event.title}" satıştan kalkar. Bekleyen rezervasyonlar da düşer. Bu işlem geri alınamaz.`}
      </Modal>
    </div>
  );
}
