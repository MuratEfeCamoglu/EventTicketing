import { CalendarX2 } from 'lucide-react';
import { useSearchParams } from 'react-router';
import { messageFor } from '../api/errors';
import { EmptyState } from '../ui/Card';
import { Pagination } from '../ui/Pagination';
import { EventCard, EventCardSkeleton } from './EventCard';
import { EventFilters } from './EventFilters';
import { eventsQueryString, filtersFromSearch, useEvents, type EventFilters as Filters } from './queries';

/** Home page: published events with search, filters and paging; the filters live in the URL. */
export function EventsPage() {
  const [search, setSearch] = useSearchParams();
  const filters = filtersFromSearch(search);
  const { data, isPending, isError, error, isFetching } = useEvents(filters);

  const apply = (next: Filters) => setSearch(new URLSearchParams(eventsQueryString(next)));

  return (
    <div>
      <section className="mb-6 rounded-2xl border border-line bg-gradient-to-br from-brand-soft via-surface to-surface px-5 py-7 sm:px-8 sm:py-10">
        <p className="text-sm font-semibold text-brand">Konser · Tiyatro · Spor · Konferans</p>
        <h1 className="mt-1 text-2xl font-bold tracking-tight sm:text-4xl">Sahnede ne var?</h1>
        <p className="mt-2 max-w-xl text-sm text-ink-soft sm:text-base">Etkinliği seçin, salondan koltuğunuzu işaretleyin; koltuklar 10 dakika sizin için ayrılır.</p>
      </section>

      <EventFilters value={filters} onChange={apply} />

      {isError ? (
        <EmptyState icon={<CalendarX2 className="size-10" />} title="Etkinlikler yüklenemedi" description={messageFor(error)} />
      ) : isPending ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }, (_, i) => (
            <EventCardSkeleton key={i} />
          ))}
        </div>
      ) : data.items.length === 0 ? (
        <EmptyState icon={<CalendarX2 className="size-10" />} title="Bu ölçütlere uyan etkinlik yok" description="Filtreleri değiştirmeyi ya da aramayı temizlemeyi deneyin." />
      ) : (
        <>
          <p className="mb-3 text-sm text-muted">{data.totalCount} etkinlik</p>
          <div className={`grid gap-4 sm:grid-cols-2 lg:grid-cols-3 ${isFetching ? 'opacity-70' : ''}`}>
            {data.items.map((event) => (
              <EventCard key={event.id} event={event} />
            ))}
          </div>
          <Pagination page={data.page} totalPages={data.totalPages} onPage={(page) => apply({ ...filters, page })} />
        </>
      )}
    </div>
  );
}
