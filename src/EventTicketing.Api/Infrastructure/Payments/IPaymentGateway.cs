namespace EventTicketing.Api.Infrastructure.Payments;

/// <summary>Outcome of a charge: succeeded with a provider reference, or declined with a failure code.</summary>
public sealed record ChargeResult(bool Succeeded, string? ProviderReference, string? FailureCode)
{
    public static ChargeResult Success(string providerReference) => new(true, providerReference, null);

    public static ChargeResult Declined(string failureCode) => new(false, null, failureCode);
}

/// <summary>Payment provider boundary (ISKELET §5.7). Only a mock exists (K-08).</summary>
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(decimal amount, string paymentToken, Guid idempotencyKey, CancellationToken cancellationToken);

    Task RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken);
}
