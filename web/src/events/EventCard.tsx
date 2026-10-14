import { CalendarDays, MapPin, Mic2, Presentation, Sparkles, Theater, Trophy } from 'lucide-react';
import { Link } from 'react-router';
import type { EventCategory, EventListItem } from '../api/types';
import { categoryLabel, formatDateShort, formatPrice, formatTime } from '../lib/format';

/** Tint and icon per category, so cards stay distinct without event images (the API has none). */
export const categoryStyle: Record<EventCategory, { icon: typeof Mic2; tint: string }> = {
  Concert: { icon: Mic2, tint: 'from-[#dbe7ff] to-[#eef3ff] text-brand' },
  Theatre: { icon: Theater, tint: 'from-[#f3e8ff] to-[#faf5ff] text-[#7c3aed]' },
  Sports: { icon: Trophy, tint: 'from-[#e2f6ee] to-[#f1fbf6] text-success' },
  Conference: { icon: Presentation, tint: 'from-[#fff4e0] to-[#fffaf0] text-warning' },
  Other: { icon: Sparkles, tint: 'from-[#eef0f4] to-[#f7f8fa] text-ink-soft' },
};

export function EventCard({ event }: { event: EventListItem }) {
  const { icon: Icon, tint } = categoryStyle[event.category];
  return (
    <Link
      to={`/etkinlik/${event.id}`}
      className="group flex flex-col overflow-hidden rounded-xl border border-line bg-surface shadow-card transition hover:-translate-y-0.5 hover:border-brand/40 hover:shadow-pop"
    >
      <div className={`relative flex h-28 items-center justify-center bg-gradient-to-br ${tint}`}>
        <Icon className="size-10 opacity-80 transition group-hover:scale-110" aria-hidden />
        <span className="absolute left-3 top-3 rounded-full bg-surface/90 px-2.5 py-0.5 text-xs font-semibold text-ink-soft">{categoryLabel(event.category)}</span>
        <span className="absolute right-3 top-3 rounded-lg bg-surface/90 px-2 py-1 text-center leading-tight shadow-card">
          <span className="block text-xs font-bold text-ink">{formatDateShort(event.startsAt)}</span>
          <span className="block text-[11px] text-muted">{formatTime(event.startsAt)}</span>
        </span>
      </div>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <h3 className="line-clamp-2 font-semibold text-ink group-hover:text-brand">{event.title}</h3>
        <p className="flex items-center gap-1.5 text-sm text-muted">
          <MapPin className="size-3.5 shrink-0" aria-hidden />
          <span className="truncate">
            {event.venueName} · {event.city}
          </span>
        </p>
        <p className="flex items-center gap-1.5 text-sm text-muted">
          <CalendarDays className="size-3.5 shrink-0" aria-hidden />
          <span className="truncate">{event.hallName}</span>
        </p>
        <div className="mt-auto flex items-center justify-between pt-2">
          <span className="text-sm font-semibold text-brand">{event.minPrice != null ? `${formatPrice(event.minPrice)}'den` : 'Fiyat yakında'}</span>
          <span className="text-xs font-medium text-muted group-hover:text-brand">Koltuk seç →</span>
        </div>
      </div>
    </Link>
  );
}

export function EventCardSkeleton() {
  return (
    <div className="overflow-hidden rounded-xl border border-line bg-surface">
      <div className="h-28 animate-pulse bg-page" />
      <div className="space-y-2 p-4">
        <div className="h-4 w-3/4 animate-pulse rounded bg-page" />
        <div className="h-3 w-1/2 animate-pulse rounded bg-page" />
        <div className="h-3 w-1/3 animate-pulse rounded bg-page" />
      </div>
    </div>
  );
}
