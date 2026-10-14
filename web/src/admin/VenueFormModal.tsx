import { useState } from 'react';
import { api } from '../api/client';
import { fieldError, messageFor } from '../api/errors';
import type { VenueListItem } from '../api/types';
import { Field, TextInput } from '../ui/Field';
import { Modal } from '../ui/Modal';

type Values = { name: string; city: string; address: string };

/** Create (no venue) or edit a venue: POST /venues or PUT /venues/{id}. */
export function VenueFormModal({ venue, onClose, onSaved }: { venue?: VenueListItem; onClose: () => void; onSaved: (venue: VenueListItem) => void }) {
  const [values, setValues] = useState<Values>({ name: venue?.name ?? '', city: venue?.city ?? '', address: venue?.address ?? '' });
  const [errors, setErrors] = useState<Partial<Values>>({});
  const [serverError, setServerError] = useState<unknown>();
  const [busy, setBusy] = useState(false);

  async function save() {
    const found: Partial<Values> = {};
    if (!values.name.trim() || values.name.trim().length > 200) found.name = 'Mekân adı girin (en fazla 200 karakter).';
    if (!values.city.trim() || values.city.trim().length > 100) found.city = 'Şehir girin (en fazla 100 karakter).';
    if (!values.address.trim() || values.address.trim().length > 500) found.address = 'Adres girin (en fazla 500 karakter).';
    setErrors(found);
    setServerError(undefined);
    if (Object.keys(found).length > 0) return;
    setBusy(true);
    try {
      const body = { name: values.name.trim(), city: values.city.trim(), address: values.address.trim() };
      const saved = venue ? await api<VenueListItem>(`/venues/${venue.id}`, { method: 'PUT', json: body }) : await api<VenueListItem>('/venues', { method: 'POST', json: body });
      onSaved(saved ?? { ...body, id: venue!.id });
    } catch (error) {
      setServerError(error);
    } finally {
      setBusy(false);
    }
  }

  const set = (key: keyof Values) => (e: React.ChangeEvent<HTMLInputElement>) => setValues((v) => ({ ...v, [key]: e.target.value }));
  const errorOf = (key: keyof Values) => errors[key] ?? fieldError(serverError, key);

  return (
    <Modal open title={venue ? 'Mekânı düzenle' : 'Yeni mekân'} confirmLabel={venue ? 'Kaydet' : 'Oluştur'} busy={busy} onConfirm={() => void save()} onClose={onClose}>
      <div className="mt-2 flex flex-col gap-3">
        <Field label="Ad" error={errorOf('name')}>{(p) => <TextInput {...p} invalid={!!errorOf('name')} value={values.name} onChange={set('name')} />}</Field>
        <Field label="Şehir" error={errorOf('city')}>{(p) => <TextInput {...p} invalid={!!errorOf('city')} value={values.city} onChange={set('city')} />}</Field>
        <Field label="Adres" error={errorOf('address')}>{(p) => <TextInput {...p} invalid={!!errorOf('address')} value={values.address} onChange={set('address')} />}</Field>
        {serverError !== undefined && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{messageFor(serverError)}</p>}
      </div>
    </Modal>
  );
}
