import { Search, SlidersHorizontal, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { allCategories, categoryLabel } from '../lib/format';
import { Button } from '../ui/Button';
import { Field, Select, TextInput } from '../ui/Field';
import type { EventFilters as Filters } from './queries';

type Props = { value: Filters; onChange: (next: Filters) => void };

/** Search box + filters; on phones the filters fold into a panel behind a "Filtreler" button. */
export function EventFilters({ value, onChange }: Props) {
  const [query, setQuery] = useState(value.q ?? '');
  const [shownQ, setShownQ] = useState(value.q);
  const [open, setOpen] = useState(false);
  // The search box follows the URL when it changes elsewhere (back button, "clear filters").
  if (value.q !== shownQ) {
    setShownQ(value.q);
    setQuery(value.q ?? '');
  }

  const update = (patch: Filters) => onChange({ ...value, ...patch, page: undefined });
  const activeCount = [value.city, value.category, value.from, value.to].filter(Boolean).length;

  function submitSearch(e: FormEvent) {
    e.preventDefault();
    update({ q: query.trim() || undefined });
  }

  return (
    <div className="mb-6 flex flex-col gap-3">
      <form onSubmit={submitSearch} className="flex gap-2" role="search">
        <label className="relative flex-1">
          <span className="sr-only">Etkinlik ara</span>
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-faint" aria-hidden />
          <TextInput className="h-11 pl-9" placeholder="Etkinlik adı ara…" value={query} onChange={(e) => setQuery(e.target.value)} />
        </label>
        <Button type="submit" className="h-11">
          Ara
        </Button>
        <Button variant="secondary" className="h-11 md:hidden" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
          <SlidersHorizontal className="size-4" />
          <span className="sr-only sm:not-sr-only">Filtreler</span>
          {activeCount > 0 && <span className="rounded-full bg-brand px-1.5 text-xs text-white">{activeCount}</span>}
        </Button>
      </form>

      <div className={`${open ? 'grid' : 'hidden'} grid-cols-2 gap-3 rounded-xl border border-line bg-surface p-4 md:grid md:grid-cols-5 md:border-0 md:bg-transparent md:p-0`}>
        <Field label="Şehir">
          {(p) => <TextInput {...p} placeholder="Tümü" value={value.city ?? ''} onChange={(e) => update({ city: e.target.value || undefined })} />}
        </Field>
        <Field label="Kategori">
          {(p) => (
            <Select {...p} value={value.category ?? ''} onChange={(e) => update({ category: e.target.value || undefined })}>
              <option value="">Tümü</option>
              {allCategories.map((c) => (
                <option key={c} value={c}>
                  {categoryLabel(c)}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Field label="Başlangıç">
          {(p) => <TextInput {...p} type="date" value={value.from?.slice(0, 10) ?? ''} onChange={(e) => update({ from: e.target.value ? new Date(`${e.target.value}T00:00`).toISOString() : undefined })} />}
        </Field>
        <Field label="Bitiş">
          {(p) => <TextInput {...p} type="date" value={value.to?.slice(0, 10) ?? ''} onChange={(e) => update({ to: e.target.value ? new Date(`${e.target.value}T23:59:59`).toISOString() : undefined })} />}
        </Field>
        <Field label="Sıralama" className="col-span-2 md:col-span-1">
          {(p) => (
            <Select {...p} value={value.sort ?? 'startsAt'} onChange={(e) => update({ sort: e.target.value === 'startsAt' ? undefined : e.target.value })}>
              <option value="startsAt">Tarih (yakın)</option>
              <option value="-startsAt">Tarih (uzak)</option>
              <option value="title">Ada göre</option>
            </Select>
          )}
        </Field>
        {activeCount > 0 && (
          <button type="button" className="col-span-2 flex items-center gap-1 text-sm font-medium text-brand md:col-span-5 md:justify-end" onClick={() => onChange({ q: value.q, sort: value.sort })}>
            <X className="size-4" /> Filtreleri temizle
          </button>
        )}
      </div>
    </div>
  );
}
