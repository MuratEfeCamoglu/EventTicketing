import { useQuery } from '@tanstack/react-query';
import { Armchair, CalendarDays, MapPin, Ticket } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import type { Paged, TicketItem } from '../api/types';
import { formatDateTime } from '../lib/format';
import { buttonClasses } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { Pagination } from '../ui/Pagination';
import { LoadingBlock } from '../ui/Spinner';
import { TicketQr } from './TicketQr';

export function TicketsPage() {
  const [page, setPage] = useState(1);
  const tickets = useQuery({ queryKey: ['tickets', page], queryFn: () => api<Paged<TicketItem>>(`/me/tickets?page=${page}&pageSize=12`) });

  return (
    <div>
      <PageHeader title="Biletlerim" subtitle="Girişte QR kodu okutun. Biletler e-postanıza da gönderildi." />
      {tickets.isPending ? (
        <LoadingBlock />
      ) : tickets.isError ? (
        <EmptyState title="Biletler yüklenemedi" description={messageFor(tickets.error)} />
      ) : tickets.data.items.length === 0 ? (
        <EmptyState
          icon={<Ticket className="size-10" />}
          title="Henüz biletiniz yok"
          description="Bir etkinlik seçip koltuğunuzu ayırdığınızda biletleriniz burada görünür."
          action={
            <Link to="/" className={buttonClasses('primary')}>
              Etkinliklere göz at
            </Link>
          }
        />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            {tickets.data.items.map((t) => (
              <Card key={t.ticketId} className="flex overflow-hidden">
                <div className="flex flex-1 flex-col gap-2 p-4 sm:p-5">
                  <Link to={`/etkinlik/${t.eventId}`} className="font-semibold hover:text-brand">
                    {t.eventTitle}
                  </Link>
                  <p className="flex items-center gap-1.5 text-sm text-muted">
                    <CalendarDays className="size-3.5" /> {formatDateTime(t.eventStartsAt)}
                  </p>
                  <p className="flex items-center gap-1.5 text-sm text-muted">
                    <MapPin className="size-3.5" /> {t.hallName}
                  </p>
                  <p className="flex items-center gap-1.5 text-sm font-semibold text-ink">
                    <Armchair className="size-3.5 text-brand" /> {t.seatLabel}
                  </p>
                  <p className="mt-auto break-all font-mono text-[11px] text-faint">{t.code}</p>
                </div>
                <div className="flex w-32 shrink-0 items-center border-l border-dashed border-line-strong bg-page p-3 sm:w-36">
                  <TicketQr ticketId={t.ticketId} label={t.seatLabel} className="w-full" />
                </div>
              </Card>
            ))}
          </div>
          <Pagination page={tickets.data.page} totalPages={tickets.data.totalPages} onPage={setPage} />
        </>
      )}
    </div>
  );
}
