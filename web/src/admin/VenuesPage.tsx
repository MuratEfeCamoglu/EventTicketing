import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, MapPin, Plus } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api/client';
import { messageFor } from '../api/errors';
import type { Paged, VenueListItem } from '../api/types';
import { Button } from '../ui/Button';
import { Card, EmptyState, PageHeader } from '../ui/Card';
import { TextInput } from '../ui/Field';
import { Pagination } from '../ui/Pagination';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';
import { VenueFormModal } from './VenueFormModal';

export function VenuesPage() {
  const [page, setPage] = useState(1);
  const [city, setCity] = useState('');
  const [creating, setCreating] = useState(false);
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = `?page=${page}&pageSize=12${city.trim() ? `&city=${encodeURIComponent(city.trim())}` : ''}`;
  const venues = useQuery({ queryKey: ['venues', page, city.trim()], queryFn: () => api<Paged<VenueListItem>>(`/venues${query}`, { anonymous: true }) });

  return (
    <div>
      <PageHeader
        title="Mekânlar ve salonlar"
        subtitle="Mekân ekleyin, mekânın içine sinema düzeninde salon tanımlayın."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" /> Yeni mekân
          </Button>
        }
      />
      <div className="mb-4 max-w-xs">
        <TextInput placeholder="Şehre göre süz" aria-label="Şehre göre süz" value={city} onChange={(e) => (setCity(e.target.value), setPage(1))} />
      </div>
      {venues.isPending ? (
        <LoadingBlock />
      ) : venues.isError ? (
        <EmptyState title="Mekânlar yüklenemedi" description={messageFor(venues.error)} />
      ) : venues.data.items.length === 0 ? (
        <EmptyState icon={<Building2 className="size-10" />} title="Mekân yok" description="İlk mekânı ekleyin." />
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            {venues.data.items.map((v) => (
              <Link key={v.id} to={`/yonetim/mekanlar/${v.id}`}>
                <Card className="flex items-start gap-3 p-4 transition hover:border-brand/40 hover:shadow-pop">
                  <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-brand-soft text-brand">
                    <Building2 className="size-5" />
                  </span>
                  <span className="min-w-0">
                    <span className="block font-semibold">{v.name}</span>
                    <span className="flex items-center gap-1 text-sm text-muted">
                      <MapPin className="size-3.5" /> {v.city}
                    </span>
                    <span className="block truncate text-xs text-faint">{v.address}</span>
                  </span>
                </Card>
              </Link>
            ))}
          </div>
          <Pagination page={venues.data.page} totalPages={venues.data.totalPages} onPage={setPage} />
        </>
      )}
      {creating && (
        <VenueFormModal
          onClose={() => setCreating(false)}
          onSaved={(v) => {
            setCreating(false);
            toast.success(`"${v.name}" oluşturuldu.`);
            void queryClient.invalidateQueries({ queryKey: ['venues'] });
          }}
        />
      )}
    </div>
  );
}
