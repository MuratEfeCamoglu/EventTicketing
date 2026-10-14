import { ArrowLeft, CalendarDays, Clock, MapPin } from 'lucide-react';
import { Link, useParams } from 'react-router';
import { ApiError, messageFor } from '../api/errors';
import { NotFoundPage } from '../app/NotFoundPage';
import { categoryLabel, formatDateTime, formatPrice, formatTime } from '../lib/format';
import { Badge } from '../ui/Badge';
import { Card, EmptyState } from '../ui/Card';
import { SeatSelection } from '../seating/SeatSelection';
import { LoadingBlock } from '../ui/Spinner';
import { categoryStyle } from './EventCard';
import { useEvent } from './queries';

export function EventPage() {
  const { id = '' } = useParams();
  const { data, isPending, error } = useEvent(id);

  if (isPending) return <LoadingBlock label="Etkinlik yükleniyor…" />;
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage title="Etkinlik bulunamadı" description="Etkinlik yayından kaldırılmış ya da hiç yayımlanmamış olabilir." />;
  }
  if (error) return <EmptyState title="Etkinlik yüklenemedi" description={messageFor(error)} />;

  const { event, hall } = data;
  const { icon: Icon, tint } = categoryStyle[event.category];

  return (
    <div className="flex flex-col gap-6">
      <Link to="/" className="flex w-fit items-center gap-1 text-sm font-medium text-muted hover:text-brand">
        <ArrowLeft className="size-4" /> Tüm etkinlikler
      </Link>

      <Card className="overflow-hidden">
        <div className="flex flex-col sm:flex-row">
          <div className={`flex h-28 items-center justify-center bg-gradient-to-br sm:h-auto sm:w-48 ${tint}`}>
            <Icon className="size-12 opacity-80" aria-hidden />
          </div>
          <div className="flex-1 p-5 sm:p-6">
            <Badge tone="info">{categoryLabel(event.category)}</Badge>
            <h1 className="mt-2 text-2xl font-bold tracking-tight sm:text-3xl">{event.title}</h1>
            <div className="mt-3 flex flex-col gap-1.5 text-sm text-ink-soft sm:flex-row sm:flex-wrap sm:gap-x-5">
              <span className="flex items-center gap-1.5">
                <CalendarDays className="size-4 text-muted" /> {formatDateTime(event.startsAt)}
              </span>
              <span className="flex items-center gap-1.5">
                <Clock className="size-4 text-muted" /> Bitiş {formatTime(event.endsAt)}
              </span>
              <span className="flex items-center gap-1.5">
                <MapPin className="size-4 text-muted" /> {event.venue.name} · {event.hall.name} · {event.venue.city}
              </span>
            </div>
            {event.description && <p className="mt-4 whitespace-pre-line text-sm leading-relaxed text-ink-soft">{event.description}</p>}
            <div className="mt-4 flex flex-wrap gap-2">
              {event.sectionPrices.map((p) => (
                <span key={p.section} className="rounded-lg border border-line bg-page px-3 py-1.5 text-sm">
                  <span className="text-muted">{p.section}</span> <strong className="text-ink">{formatPrice(p.price)}</strong>
                </span>
              ))}
            </div>
          </div>
        </div>
      </Card>

      <SeatSelection eventId={event.id} hall={hall} />
    </div>
  );
}
