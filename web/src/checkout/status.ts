import type { ReservationStatus } from '../api/types';
import type { Tone } from '../ui/Badge';

export const reservationStatus: Record<ReservationStatus, { label: string; tone: Tone }> = {
  Pending: { label: 'Ödeme bekliyor', tone: 'warning' },
  Confirmed: { label: 'Onaylandı', tone: 'success' },
  Expired: { label: 'Süresi doldu', tone: 'neutral' },
  Cancelled: { label: 'İptal edildi', tone: 'neutral' },
  PaymentFailed: { label: 'Ödeme reddedildi', tone: 'danger' },
};
