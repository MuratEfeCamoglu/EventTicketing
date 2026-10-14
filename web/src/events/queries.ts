import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { EventDetail, EventListItem, HallDetail, Paged } from '../api/types';

export type EventFilters = { city?: string; category?: string; from?: string; to?: string; q?: string; sort?: string; page?: number; pageSize?: number };

const keys: (keyof EventFilters)[] = ['city', 'category', 'from', 'to', 'q', 'sort', 'page', 'pageSize'];

/** Filters → "?city=…&page=2"; empty values are left out (also used for the page URL). */
export function eventsQueryString(filters: EventFilters): string {
  const params = new URLSearchParams();
  for (const key of keys) {
    const value = filters[key];
    if (value !== undefined && value !== '' && value !== null) params.set(key, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : '';
}

export function filtersFromSearch(search: URLSearchParams): EventFilters {
  const filters: EventFilters = {};
  for (const key of keys) {
    const value = search.get(key);
    if (!value) continue;
    if (key === 'page' || key === 'pageSize') filters[key] = Number(value);
    else filters[key] = value;
  }
  return filters;
}

export function useEvents(filters: EventFilters) {
  return useQuery({
    queryKey: ['events', filters],
    queryFn: () => api<Paged<EventListItem>>(`/events${eventsQueryString({ pageSize: 12, ...filters })}`, { anonymous: true }),
    placeholderData: keepPreviousData,
  });
}

/** Published event with its hall layout (the hall gives the section order for the seat map). */
export function useEvent(id: string) {
  return useQuery({
    queryKey: ['event', id],
    queryFn: async () => {
      const event = await api<EventDetail>(`/events/${id}`, { anonymous: true });
      const hall = await api<HallDetail>(`/halls/${event.hall.id}`, { anonymous: true });
      return { event, hall };
    },
  });
}
