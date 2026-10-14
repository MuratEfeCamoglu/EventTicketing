import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { api } from '../api/client';
import { ApiError, messageFor } from '../api/errors';
import type { Paged, Reservation, ReservationListItem } from '../api/types';
import { useToast } from '../ui/Toast';

/**
 * POST /events/{id}/reservations and its outcomes (design §6.4): success opens the reservation page; seat.unavailable
 * drops the lost seats and refreshes the map; already_pending opens the reservation the customer already has.
 */
export function useHoldSeats(eventId: string, onSeatsLost: (ids: string[]) => void) {
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (eventSeatIds: string[]) => api<Reservation>(`/events/${eventId}/reservations`, { method: 'POST', json: { eventSeatIds } }),
    onSuccess: (reservation) => {
      queryClient.setQueryData(['reservation', reservation.reservationId], reservation);
      void queryClient.invalidateQueries({ queryKey: ['seats', eventId] });
      navigate(`/rezervasyon/${reservation.reservationId}`);
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.code === 'seat.unavailable') {
        const lost = (error.extensions.unavailableSeatIds as string[] | undefined) ?? [];
        onSeatsLost(lost);
        toast.error(messageFor(error));
        await queryClient.invalidateQueries({ queryKey: ['seats', eventId] });
        return;
      }
      if (error instanceof ApiError && error.code === 'reservation.already_pending') {
        const mine = await api<Paged<ReservationListItem>>('/me/reservations?pageSize=100').catch(() => null);
        const pending = mine?.items.find((r) => r.eventId === eventId && r.status === 'Pending');
        toast.error(messageFor(error));
        if (pending) navigate(`/rezervasyon/${pending.reservationId}`);
        return;
      }
      toast.error(messageFor(error));
    },
  });
}
