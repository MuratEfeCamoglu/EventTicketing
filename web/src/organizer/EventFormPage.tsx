import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Save } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { ApiError, fieldError, messageFor } from '../api/errors';
import type { EventDetail, HallDetail, Paged, VenueDetail, VenueListItem } from '../api/types';
import { NotFoundPage } from '../app/NotFoundPage';
import { allCategories, categoryLabel } from '../lib/format';
import { Button } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { Field, Select, TextArea, TextInput } from '../ui/Field';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';
import { eventStatus } from './MyEventsPage';
import { emptyEventForm, fromEvent, toEventRequest, validateEventForm, type EventFormValues } from './eventForm';

/** Create (no id) or edit (id) an event. Editing reads GET /organizer/events/{id}, which also returns drafts (AC-21.1). */
export function EventFormPage() {
  const { id } = useParams();
  const editing = !!id;
  const existing = useQuery({
    queryKey: ['organizer-event', id],
    queryFn: async () => {
      const event = await api<EventDetail>(`/organizer/events/${id}`);
      const hall = await api<HallDetail>(`/halls/${event.hall.id}`, { anonymous: true });
      return { event, hall };
    },
    enabled: editing,
  });

  if (editing && existing.isPending) return <LoadingBlock label="Etkinlik yükleniyor…" />;
  if (editing && existing.error instanceof ApiError && existing.error.status === 404) return <NotFoundPage title="Etkinlik bulunamadı" description="Bu etkinlik size ait değil ya da silinmiş." />;
  if (editing && existing.isError) return <EmptyState title="Etkinlik yüklenemedi" description={messageFor(existing.error)} />;

  return <EventForm key={id ?? 'new'} eventId={id} initial={existing.data ? fromEvent(existing.data.event, existing.data.hall) : emptyEventForm} status={existing.data?.event.status} />;
}

function EventForm({ eventId, initial, status }: { eventId?: string; initial: EventFormValues; status?: EventDetail['status'] }) {
  const [values, setValues] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<unknown>();
  const [saving, setSaving] = useState(false);
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();

  const venues = useQuery({ queryKey: ['venues', 'all'], queryFn: () => api<Paged<VenueListItem>>('/venues?pageSize=100', { anonymous: true }) });
  const venue = useQuery({ queryKey: ['venue', values.venueId], queryFn: () => api<VenueDetail>(`/venues/${values.venueId}`, { anonymous: true }), enabled: !!values.venueId });
  const hall = useQuery({ queryKey: ['hall', values.hallId], queryFn: () => api<HallDetail>(`/halls/${values.hallId}`, { anonymous: true }), enabled: !!values.hallId });
  // Undefined until the chosen hall is loaded; saving waits for it (validateEventForm).
  const sections = hall.data?.sections.map((s) => s.name);

  const set = <K extends keyof EventFormValues>(key: K, value: EventFormValues[K]) => setValues((v) => ({ ...v, [key]: value }));
  const errorOf = (key: string, apiKey = key) => errors[key] ?? fieldError(serverError, apiKey);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const found = validateEventForm(values, sections);
    setErrors(found);
    setServerError(undefined);
    if (Object.keys(found).length > 0) return;
    setSaving(true);
    try {
      const request = toEventRequest(values, sections ?? []);
      if (eventId) await api(`/events/${eventId}`, { method: 'PUT', json: request });
      else await api('/events', { method: 'POST', json: request });
      toast.success(eventId ? 'Etkinlik güncellendi.' : 'Etkinlik taslak olarak oluşturuldu.');
      await queryClient.invalidateQueries({ queryKey: ['organizer-events'] });
      void queryClient.invalidateQueries({ queryKey: ['organizer-event', eventId] });
      void queryClient.invalidateQueries({ queryKey: ['events'] });
      navigate('/yonetim/etkinlikler');
    } catch (error) {
      setServerError(error);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <Link to="/yonetim/etkinlikler" className="mb-3 flex w-fit items-center gap-1 text-sm font-medium text-muted hover:text-brand">
        <ArrowLeft className="size-4" /> Etkinliklerim
      </Link>
      <PageHeader title={eventId ? 'Etkinliği düzenle' : 'Yeni etkinlik'} subtitle={status ? `Durum: ${eventStatus[status].label}` : 'Etkinlik önce taslak olarak kaydedilir; yayımlayınca satışa açılır.'} />
      <Card className="p-5 sm:p-6">
        <form className="grid gap-4 sm:grid-cols-2" onSubmit={submit} noValidate>
          <Field label="Başlık" error={errorOf('title')} className="sm:col-span-2">
            {(p) => <TextInput {...p} invalid={!!errorOf('title')} maxLength={200} value={values.title} onChange={(e) => set('title', e.target.value)} />}
          </Field>
          <Field label="Açıklama" error={errorOf('description')} className="sm:col-span-2">
            {(p) => <TextArea {...p} invalid={!!errorOf('description')} maxLength={4000} value={values.description} onChange={(e) => set('description', e.target.value)} />}
          </Field>
          <Field label="Kategori">
            {(p) => (
              <Select {...p} value={values.category} onChange={(e) => set('category', e.target.value as EventFormValues['category'])}>
                {allCategories.map((c) => (
                  <option key={c} value={c}>
                    {categoryLabel(c)}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <div className="hidden sm:block" />
          <Field label="Mekân">
            {(p) => (
              <Select {...p} value={values.venueId} onChange={(e) => setValues((v) => ({ ...v, venueId: e.target.value, hallId: '', prices: {} }))}>
                <option value="">{venues.isPending ? 'Yükleniyor…' : 'Mekân seçin'}</option>
                {venues.data?.items.map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.name} · {v.city}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <Field label="Salon" error={errorOf('hallId')}>
            {(p) => (
              <Select {...p} invalid={!!errorOf('hallId')} disabled={!values.venueId} value={values.hallId} onChange={(e) => setValues((v) => ({ ...v, hallId: e.target.value, prices: {} }))}>
                <option value="">{values.venueId ? 'Salon seçin' : 'Önce mekân seçin'}</option>
                {venue.data?.halls.map((h) => (
                  <option key={h.id} value={h.id}>
                    {h.name} · {h.seatCount} koltuk
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <Field label="Başlangıç" error={errorOf('startsAt')}>
            {(p) => <TextInput {...p} invalid={!!errorOf('startsAt')} type="datetime-local" value={values.startsAt} onChange={(e) => set('startsAt', e.target.value)} />}
          </Field>
          <Field label="Bitiş" error={errorOf('endsAt')}>
            {(p) => <TextInput {...p} invalid={!!errorOf('endsAt')} type="datetime-local" value={values.endsAt} onChange={(e) => set('endsAt', e.target.value)} />}
          </Field>

          <fieldset className="sm:col-span-2">
            <legend className="mb-2 text-sm font-medium text-ink-soft">Bölüm fiyatları</legend>
            {!values.hallId ? (
              <p className="rounded-lg bg-page px-3 py-3 text-sm text-muted">Salon seçince bölümler burada listelenir.</p>
            ) : hall.isPending ? (
              <p className="text-sm text-muted">Bölümler yükleniyor…</p>
            ) : !sections ? (
              <p className="rounded-lg bg-danger-soft px-3 py-3 text-sm text-danger">Salonun bölümleri yüklenemedi: {messageFor(hall.error)}</p>
            ) : (
              <div className="grid gap-3 sm:grid-cols-3">
                {sections.map((section) => (
                  <Field key={section} label={section} error={errors[`price:${section}`]}>
                    {(p) => (
                      <div className="relative">
                        <TextInput
                          {...p}
                          invalid={!!errors[`price:${section}`]}
                          inputMode="decimal"
                          placeholder="0,00"
                          className="pr-8"
                          value={values.prices[section] ?? ''}
                          onChange={(e) => setValues((v) => ({ ...v, prices: { ...v.prices, [section]: e.target.value } }))}
                        />
                        <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-muted">₺</span>
                      </div>
                    )}
                  </Field>
                ))}
              </div>
            )}
          </fieldset>

          {serverError !== undefined && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger sm:col-span-2">{messageFor(serverError)}</p>}
          <div className="flex justify-end gap-2 sm:col-span-2">
            <Link to="/yonetim/etkinlikler" className="inline-flex h-10 items-center rounded-lg px-4 text-sm font-semibold text-ink-soft hover:bg-page">
              Vazgeç
            </Link>
            <Button type="submit" loading={saving} disabled={!!values.hallId && hall.isPending}>
              <Save className="size-4" /> {eventId ? 'Değişiklikleri kaydet' : 'Taslak olarak kaydet'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
}
