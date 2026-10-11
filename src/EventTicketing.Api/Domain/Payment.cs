namespace EventTicketing.Api.Domain;

/// <summary>Immutable once written; a refund is a separate row (ISKELET §5.5).</summary>
public sealed class Payment
{
    private Payment()
    {
    }

    public Payment(
        Guid reservationId,
        Guid idempotencyKey,
        decimal amount,
        PaymentStatus status,
        string? providerReference,
        string? failureCode,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        ReservationId = reservationId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Status = status;
        ProviderReference = providerReference;
        FailureCode = failureCode;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
