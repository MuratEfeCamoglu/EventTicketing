import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Armchair, MapPin, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { api } from '../api/client';
import { ApiError, messageFor } from '../api/errors';
import type { VenueDetail } from '../api/types';
import { NotFoundPage } from '../app/NotFoundPage';
import { Button, buttonClasses } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';
import { VenueFormModal } from './VenueFormModal';

export function VenueDetailPage() {
  const { id = '' } = useParams();
  const [editing, setEditing] = useState(false);
  const toast = useToast();
  const queryClient = useQueryClient();
  const venue = useQuery({ queryKey: ['venue', id], queryFn: () => api<VenueDetail>(`/venues/${id}`, { anonymous: true }) });

  if (venue.isPending) return <LoadingBlock />;
  if (venue.error instanceof ApiError && venue.error.status === 404) return <NotFoundPage title="Mekân bulunamadı" />;
  if (venue.isError) return <EmptyState title="Mekân yüklenemedi" description={messageFor(venue.error)} />;
  const v = venue.data;

  return (
    <div>
      <Link to="/yonetim/mekanlar" className="mb-3 flex w-fit items-center gap-1 text-sm font-medium text-muted hover:text-brand">
        <ArrowLeft className="size-4" /> Mekânlar
      </Link>
      <PageHeader
        title={v.name}
        subtitle={
          <span className="flex items-center gap-1">
            <MapPin className="size-3.5" /> {v.address} · {v.city}
          </span>
        }
        actions={
          <>
            <Button variant="secondary" onClick={() => setEditing(true)}>
              <Pencil className="size-4" /> Düzenle
            </Button>
            <Link to={`/yonetim/mekanlar/${v.id}/salon-ekle`} className={buttonClasses('primary')}>
              <Plus className="size-4" /> Salon ekle
            </Link>
          </>
        }
      />
      <h2 className="mb-3 text-lg font-semibold">Salonlar</h2>
      {v.halls.length === 0 ? (
        <EmptyState icon={<Armchair className="size-10" />} title="Bu mekânda salon yok" description="Etkinlik oluşturmak için önce bir salon ekleyin." />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {v.halls.map((h) => (
            <Card key={h.id} className="flex items-center gap-3 p-4">
              <span className="grid size-10 place-items-center rounded-lg bg-page text-ink-soft">
                <Armchair className="size-5" />
              </span>
              <span>
                <span className="block font-semibold">{h.name}</span>
                <span className="text-sm text-muted">{h.seatCount.toLocaleString('tr-TR')} koltuk</span>
              </span>
            </Card>
          ))}
        </div>
      )}
      {editing && (
        <VenueFormModal
          venue={v}
          onClose={() => setEditing(false)}
          onSaved={() => {
            setEditing(false);
            toast.success('Mekân güncellendi.');
            void queryClient.invalidateQueries({ queryKey: ['venue', id] });
            void queryClient.invalidateQueries({ queryKey: ['venues'] });
          }}
        />
      )}
    </div>
  );
}
