namespace EventTicketing.Api.Domain;

public enum UserRole
{
    Customer,
    Organizer,
    Admin,
}

public enum EventCategory
{
    Concert,
    Theatre,
    Sports,
    Conference,
    Other,
}

public enum EventStatus
{
    Draft,
    Published,
    Cancelled,
}

public enum EventSeatStatus
{
    Available,
    Held,
    Sold,
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    PaymentFailed,
    Cancelled,
    Expired,
}

public enum PaymentStatus
{
    Succeeded,
    Failed,
    Refunded,
}
