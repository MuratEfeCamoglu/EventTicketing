import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Plus, Save, Trash2 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import { SeatMap } from '../seating/SeatMap';
import { Button } from '../ui/Button';
import { Card, PageHeader } from '../ui/Card';
import { Field, TextInput } from '../ui/Field';
import { useToast } from '../ui/Toast';
import { buildHallRequest, safePreviewSeats, type SectionDraft } from './hallBuilder';

const starter: SectionDraft[] = [
  { name: 'Salon', rows: 'A-L', seatsPerRow: 20 },
  { name: 'Balkon', rows: 'A-F', seatsPerRow: 18 },
];

const noSelection = new Set<string>();
const sectionCollator = new Intl.Collator('tr', { numeric: true });

/** Admin hall builder (design §7): sections as "row range × seats per row", live total and a cinema-style preview. */
export function HallBuilderPage() {
  const { id: venueId = '' } = useParams();
  const [name, setName] = useState('');
  const [sections, setSections] = useState<SectionDraft[]>(starter);
  const [showErrors, setShowErrors] = useState(false);
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState<unknown>();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();

  const { request, seatCount, errors } = useMemo(() => buildHallRequest(name, sections), [name, sections]);
  const preview = useMemo(() => safePreviewSeats(request, seatCount), [request, seatCount]);
  // The event seat map shows sections in the order GET /halls/{id} returns them: by name.
  const order = useMemo(() => request.sections.map((s) => s.name).sort(sectionCollator.compare), [request]);

  const update = (index: number, patch: Partial<SectionDraft>) => setSections((list) => list.map((s, i) => (i === index ? { ...s, ...patch } : s)));

  async function save() {
    setShowErrors(true);
    setServerError(undefined);
    if (errors.length > 0) return;
    setSaving(true);
    try {
      await api(`/venues/${venueId}/halls`, { method: 'POST', json: request });
      toast.success(`"${request.name}" salonu oluşturuldu (${seatCount.toLocaleString('tr-TR')} koltuk).`);
      await queryClient.invalidateQueries({ queryKey: ['venue', venueId] });
      navigate(`/yonetim/mekanlar/${venueId}`);
    } catch (error) {
      setServerError(error);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <Link to={`/yonetim/mekanlar/${venueId}`} className="mb-3 flex w-fit items-center gap-1 text-sm font-medium text-muted hover:text-brand">
        <ArrowLeft className="size-4" /> Mekân
      </Link>
      <PageHeader title="Salon ekle" subtitle="Her bölüm için sıra aralığını (A-L ya da 1-12) ve sıra başına koltuk sayısını girin. Haritada bölümler ada göre sıralanır." />
      <div className="grid gap-6 xl:grid-cols-[400px_minmax(0,1fr)]">
        <Card className="flex flex-col gap-4 p-5">
          <Field label="Salon adı">{(p) => <TextInput {...p} maxLength={100} placeholder="ör. Büyük Salon" value={name} onChange={(e) => setName(e.target.value)} />}</Field>
          <div className="flex flex-col gap-3">
            {sections.map((section, index) => (
              <div key={index} className="rounded-xl border border-line bg-page p-3">
                <div className="mb-2 flex items-center justify-between">
                  <span className="text-xs font-semibold text-muted">{index + 1}. bölüm</span>
                  {sections.length > 1 && (
                    <button type="button" aria-label={`${index + 1}. bölümü kaldır`} onClick={() => setSections((list) => list.filter((_, i) => i !== index))} className="rounded p-1 text-faint hover:bg-danger-soft hover:text-danger">
                      <Trash2 className="size-4" />
                    </button>
                  )}
                </div>
                <div className="grid grid-cols-[minmax(0,1fr)_88px_88px] gap-2">
                  <Field label="Ad">{(p) => <TextInput {...p} maxLength={50} value={section.name} onChange={(e) => update(index, { name: e.target.value })} />}</Field>
                  <Field label="Sıralar">{(p) => <TextInput {...p} placeholder="A-L" value={section.rows} onChange={(e) => update(index, { rows: e.target.value })} />}</Field>
                  <Field label="Koltuk/sıra">
                    {(p) => <TextInput {...p} type="number" min={1} inputMode="numeric" value={Number.isFinite(section.seatsPerRow) ? section.seatsPerRow : ''} onChange={(e) => update(index, { seatsPerRow: e.target.valueAsNumber })} />}
                  </Field>
                </div>
              </div>
            ))}
            <Button variant="secondary" onClick={() => setSections((list) => [...list, { name: '', rows: 'A-E', seatsPerRow: 10 }])}>
              <Plus className="size-4" /> Bölüm ekle
            </Button>
          </div>
          <div className="flex items-center justify-between rounded-lg bg-brand-soft px-3 py-2 text-sm">
            <span className="text-brand">Toplam</span>
            <strong className={seatCount > 5000 ? 'text-danger' : 'text-brand'}>{seatCount.toLocaleString('tr-TR')} / 5.000 koltuk</strong>
          </div>
          {showErrors && errors.length > 0 && (
            <ul role="alert" className="list-inside list-disc rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
              {errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
          {serverError !== undefined && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{messageFor(serverError)}</p>}
          <Button onClick={() => void save()} loading={saving}>
            <Save className="size-4" /> Salonu kaydet
          </Button>
        </Card>
        <div className="min-w-0">
          <h2 className="mb-3 text-lg font-semibold">Önizleme</h2>
          {preview.length > 0 && seatCount <= 5000 ? (
            <SeatMap seats={preview} sectionOrder={order} selectedIds={noSelection} readOnly />
          ) : (
            <p className="rounded-xl border border-dashed border-line-strong bg-surface px-4 py-10 text-center text-sm text-muted">Geçerli bölümler girildiğinde salon burada görünür.</p>
          )}
        </div>
      </div>
    </div>
  );
}
