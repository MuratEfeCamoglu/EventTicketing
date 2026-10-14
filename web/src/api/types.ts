// TypeScript shapes of the API responses (ISKELET §5.13); enums are serialised as strings.

export type EventCategory = 'Concert' | 'Theatre' | 'Sports' | 'Conference' | 'Other';
export type EventStatus = 'Draft' | 'Published' | 'Cancelled';
export type SeatStatus = 'Available' | 'Held' | 'Sold';
export type ReservationStatus = 'Pending' | 'Confirmed' | 'Expired' | 'Cancelled' | 'PaymentFailed';
export type PaymentStatus = 'Succeeded' | 'Failed' | 'Refunded';

export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type EventListItem = {
  id: string;
  title: string;
  category: EventCategory;
  startsAt: string;
  endsAt: string;
  venueName: string;
  city: string;
  hallName: string;
  minPrice: number | null;
};

export type OrganizerEventItem = EventListItem & { organizerId: string; status: EventStatus };

export type SectionPrice = { section: string; price: number };

/** GET /events/{id} and GET /organizer/events/{id}. */
export type EventDetail = {
  id: string;
  title: string;
  description: string;
  category: EventCategory;
  startsAt: string;
  endsAt: string;
  status: EventStatus;
  venue: { id: string; name: string; city: string; address: string };
  hall: { id: string; name: string };
  sectionPrices: SectionPrice[];
};

export type SeatDto = { eventSeatId: string; section: string; row: string; number: number; price: number; status: SeatStatus };

export type HallDetail = {
  id: string;
  venueId: string;
  name: string;
  sections: { name: string; rows: { label: string; seats: { id: string; number: number }[] }[] }[];
};

export type VenueListItem = { id: string; name: string; city: string; address: string };
export type VenueDetail = VenueListItem & { halls: { id: string; name: string; seatCount: number }[] };

export type ReservedSeat = { eventSeatId: string; section: string; row: string; number: number; price: number };

export type Reservation = {
  reservationId: string;
  eventId: string;
  status: ReservationStatus;
  expiresAt: string;
  totalAmount: number;
  createdAt: string;
  seats: ReservedSeat[];
};

export type ReservationListItem = {
  reservationId: string;
  eventId: string;
  eventTitle: string;
  eventStartsAt: string;
  status: ReservationStatus;
  totalAmount: number;
  expiresAt: string;
  createdAt: string;
  seatCount: number;
};

export type PaymentResult = {
  paymentId: string;
  status: PaymentStatus;
  tickets: { ticketId: string; eventSeatId: string; code: string; seatLabel: string }[];
};

export type TicketItem = {
  ticketId: string;
  code: string;
  reservationId: string;
  eventId: string;
  eventTitle: string;
  eventStartsAt: string;
  hallName: string;
  seatLabel: string;
  issuedAt: string;
};

export type RegisteredUser = { id: string; email: string; fullName: string; role: string };

export type TokenResponse = { accessToken: string; accessTokenExpiresAt: string; refreshToken: string };
