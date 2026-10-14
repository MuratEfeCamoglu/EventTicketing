import { useQuery } from '@tanstack/react-query';
import { QrCode } from 'lucide-react';
import { apiBlob } from '../api/client';
import { cx } from '../ui/cx';

/** The ticket's QR PNG; the endpoint needs the bearer token, so it is fetched as a blob (design §6.5). */
export function TicketQr({ ticketId, label, className }: { ticketId: string; label: string; className?: string }) {
  const qr = useQuery({
    queryKey: ['qr', ticketId],
    queryFn: async () => URL.createObjectURL(await apiBlob(`/tickets/${ticketId}/qr`)),
    staleTime: Infinity,
    gcTime: Infinity,
  });

  return (
    <div className={cx('grid aspect-square place-items-center overflow-hidden rounded-lg border border-line bg-white', className)}>
      {qr.data ? (
        <img src={qr.data} alt={`${label} bileti QR kodu`} className="size-full object-contain p-1.5 [image-rendering:pixelated]" />
      ) : (
        <QrCode className={cx('size-1/2 text-line-strong', qr.isPending && 'animate-pulse')} aria-label={qr.isError ? 'QR yüklenemedi' : 'QR yükleniyor'} />
      )}
    </div>
  );
}
