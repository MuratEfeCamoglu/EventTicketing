import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clock, Mail, TimerOff, XCircle } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { ApiError, messageFor } from '../api/errors';
import type { EventDetail, PaymentResult, Reservation } from '../api/types';
import { TicketQr } from '../account/TicketQr';
import { NotFoundPage } from '../app/NotFoundPage';
import { formatDateTime, formatPrice } from '../lib/format';
import { seatLabel } from '../seating/layout';
import { Badge } from '../ui/Badge';
import { Button, buttonClasses } from '../ui/Button';
import { Card, EmptyState } from '../ui/Card';
import { Modal } from '../ui/Modal';
import { LoadingBlock } from '../ui/Spinner';
import { useToast } from '../ui/Toast';
import { cx } from '../ui/cx';
import { useAuth } from '../auth/AuthProvider';
import { paymentOutcome, reservationRefetchInterval } from './outcomes';
import { PaymentForm } from './PaymentForm';
import { reservationStatus } from './status';
import { useCountdown } from './useCountdown';
import { usePayment, type PaymentToken } from './usePayment';

function Countdown({ label, expired, secondsLeft }: { label: string; expired: boolean; secondsLeft: number }) {
  return (
    <div className={cx('flex items-center justify-between gap-3 rounded-xl border px-4 py-3', secondsLeft <= 60 ? 'border-danger/30 bg-danger-soft text-danger' : 'border-warning-line bg-warning-soft text-warning')}>
      <span className="flex items-center gap-2 text-sm font-semibold">
        <Clock className="size-4" /> {expired ? 'Süre doldu' : 'Koltuklarınız sizin için ayrıldı'}
      </span>
      <span className="font-mono text-xl font-bold tabular-nums" aria-live="off">
        {label}
      </span>
    </div>
  );
}

const EPOCH = '1970-01-01T00:00:00Z';

function SuccessView({ result, reservation }: { result: PaymentResult; reservation: Reservation }) {
  return (
    <div className="flex flex-col gap-6">
      <Card className="flex flex-col items-center gap-2 p-8 text-center">
        <CheckCircle2 className="size-12 text-success" />
        <h1 className="text-2xl font-bold">Ödeme başarılı</h1>
        <p className="flex items-center gap-1.5 text-sm text-muted">
          <Mail className="size-4" /> Biletleriniz e-posta adresinize de gönderildi.
        </p>
        <p className="text-sm text-muted">Toplam {formatPrice(reservation.totalAmount)}</p>
      </Card>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {result.tickets.map((t) => (
          <Card key={t.ticketId} className="flex items-center gap-4 p-4">
            <TicketQr ticketId={t.ticketId} label={t.seatLabel} className="w-28 shrink-0" />
            <div className="min-w-0">
              <p className="font-semibold">{t.seatLabel}</p>
              <p className="mt-1 break-all font-mono text-xs text-muted">{t.code}</p>
            </div>
          </Card>
        ))}
      </div>
      <div className="flex flex-wrap justify-center gap-2">
        <Link to="/biletlerim" className={buttonClasses('primary')}>
          Biletlerime git
        </Link>
        <Link to="/" className={buttonClasses('secondary')}>
          Başka etkinliklere bak
        </Link>
      </div>
    </div>
  );
}

function OutcomeCard({ icon, title, text, eventId }: { icon: React.ReactNode; title: string; text: string; eventId: string }) {
  return (
    <Card className="mx-auto flex max-w-lg flex-col items-center gap-3 p-8 text-center">
      {icon}
      <h1 className="text-xl font-bold">{title}</h1>
      <p className="text-sm text-muted">{text}</p>
      <div className="mt-2 flex flex-wrap justify-center gap-2">
        <Link to={`/etkinlik/${eventId}`} className={buttonClasses('primary')}>
          Etkinliğe dön
        </Link>
        <Link to="/rezervasyonlarim" className={buttonClasses('secondary')}>
          Rezervasyonlarım
        </Link>
      </div>
    </Card>
  );
}

export function ReservationPage() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  const { session } = useAuth();
  const expiredLocally = useRef(false);
  const reservation = useQuery({
    queryKey: ['reservation', id],
    queryFn: () => api<Reservation>(`/reservations/${id}`),
    refetchInterval: (query) => reservationRefetchInterval(query.state.data?.status, expiredLocally.current),
  });
  const eventId = reservation.data?.eventId;
  const event = useQuery({
    queryKey: ['event-summary', eventId],
    queryFn: () => api<EventDetail>(`/events/${eventId}`, { anonymous: true }),
    enabled: !!eventId,
    retry: false,
  });
  const payment = usePayment(id);
  const [result, setResult] = useState<PaymentResult>();
  const [failure, setFailure] = useState<ApiError>();
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  // Time left comes from the server's expiresAt; at zero the reservation is read again and shows as expired.
  const countdown = useCountdown(reservation.data?.expiresAt ?? EPOCH);
  const refreshedAtExpiry = useRef(false);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['reservation', id] });

  // At zero the reservation is read again; while the server still says Pending (browser clock ahead), the query keeps
  // re-reading every 2 s via refetchInterval until the server reports Expired.
  useEffect(() => {
    expiredLocally.current = countdown.expired;
    if (countdown.expired && reservation.data?.status === 'Pending' && !refreshedAtExpiry.current) {
      refreshedAtExpiry.current = true;
      void queryClient.invalidateQueries({ queryKey: ['reservation', id] });
    }
  }, [countdown.expired, reservation.data?.status, queryClient, id]);

  async function pay(token: PaymentToken) {
    try {
      const paid = await payment.pay(token);
      setResult(paid);
      void queryClient.invalidateQueries({ queryKey: ['seats'] });
      void queryClient.invalidateQueries({ queryKey: ['tickets'] });
      refresh();
    } catch (error) {
      if (error instanceof ApiError) {
        const outcome = paymentOutcome(error);
        if (outcome.kind === 'message') toast.error(outcome.message);
        else setFailure(error);
        void queryClient.invalidateQueries({ queryKey: ['seats'] });
        refresh();
      } else {
        toast.error(`${messageFor(error)} Aynı ödemeyi güvenle tekrar deneyebilirsiniz.`);
      }
    }
  }

  async function cancel() {
    setCancelling(true);
    try {
      await api(`/reservations/${id}`, { method: 'DELETE' });
      toast.success('Rezervasyon iptal edildi, koltuklar serbest bırakıldı.');
      void queryClient.invalidateQueries({ queryKey: ['seats'] });
      navigate(reservation.data ? `/etkinlik/${reservation.data.eventId}` : '/');
    } catch (error) {
      toast.error(messageFor(error));
      refresh();
    } finally {
      setCancelling(false);
      setConfirmCancel(false);
    }
  }

  if (reservation.isPending) return <LoadingBlock label="Rezervasyon yükleniyor…" />;
  if (reservation.error instanceof ApiError && reservation.error.status === 404) return <NotFoundPage title="Rezervasyon bulunamadı" description="Bu rezervasyon size ait değil ya da hiç oluşturulmadı." />;
  if (reservation.isError) return <EmptyState title="Rezervasyon yüklenemedi" description={messageFor(reservation.error)} />;

  const r = reservation.data;
  if (result) return <SuccessView result={result} reservation={r} />;
  if (failure?.code === 'payment.declined' || r.status === 'PaymentFailed') {
    return <OutcomeCard icon={<XCircle className="size-12 text-danger" />} title="Ödeme reddedildi" text="Kartınız reddedildi ve koltuklar serbest bırakıldı. Yeniden denemek için koltukları tekrar seçin." eventId={r.eventId} />;
  }
  if (failure?.code === 'reservation.expired' || r.status === 'Expired') {
    return <OutcomeCard icon={<TimerOff className="size-12 text-muted" />} title="Rezervasyon süresi doldu" text="10 dakika içinde ödeme yapılmadığı için koltuklar serbest bırakıldı." eventId={r.eventId} />;
  }
  if (r.status === 'Cancelled') {
    return <OutcomeCard icon={<XCircle className="size-12 text-muted" />} title="Rezervasyon iptal edildi" text="Bu rezervasyon iptal edildi; koltuklar başkaları için serbest." eventId={r.eventId} />;
  }
  if (r.status === 'Confirmed') {
    return <OutcomeCard icon={<CheckCircle2 className="size-12 text-success" />} title="Bu rezervasyon ödendi" text="Biletlerinizi Biletlerim sayfasında QR kodlarıyla görebilirsiniz." eventId={r.eventId} />;
  }

  const status = reservationStatus[r.status];
  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <Countdown {...countdown} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
        <Card className="p-5 sm:p-6">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-sm text-muted">Rezervasyon</p>
              <h1 className="text-xl font-bold">{event.data?.title ?? 'Etkinlik'}</h1>
              {event.data && (
                <p className="mt-1 text-sm text-ink-soft">
                  {formatDateTime(event.data.startsAt)} · {event.data.venue.name} · {event.data.hall.name}
                </p>
              )}
            </div>
            <Badge tone={status.tone}>{status.label}</Badge>
          </div>
          <ul className="mt-5 flex flex-col divide-y divide-line border-y border-line">
            {r.seats.map((s) => (
              <li key={s.eventSeatId} className="flex justify-between py-2.5 text-sm">
                <span>{seatLabel(s)}</span>
                <span className="font-medium">{formatPrice(s.price)}</span>
              </li>
            ))}
          </ul>
          <div className="mt-3 flex justify-between text-base font-bold">
            <span>Toplam</span>
            <span>{formatPrice(r.totalAmount)}</span>
          </div>
          {session?.role === 'Customer' && (
            <Button variant="dangerGhost" className="mt-5" onClick={() => setConfirmCancel(true)}>
              Rezervasyonu iptal et
            </Button>
          )}
        </Card>
        <Card className="p-5 sm:p-6">
          <h2 className="mb-4 text-lg font-semibold">Ödeme</h2>
          {session?.role === 'Customer' ? (
            <PaymentForm total={r.totalAmount} pending={payment.pending} disabled={countdown.expired} onPay={(t) => void pay(t)} />
          ) : (
            <p className="rounded-lg bg-brand-soft px-3 py-2.5 text-sm text-brand">Ödemeyi yalnız rezervasyonun sahibi olan müşteri yapabilir.</p>
          )}
        </Card>
      </div>
      <Modal open={confirmCancel} title="Rezervasyon iptal edilsin mi?" confirmLabel="İptal et" tone="danger" busy={cancelling} onConfirm={() => void cancel()} onClose={() => setConfirmCancel(false)}>
        Koltuklar hemen serbest bırakılır ve başkaları tarafından alınabilir.
      </Modal>
    </div>
  );
}
