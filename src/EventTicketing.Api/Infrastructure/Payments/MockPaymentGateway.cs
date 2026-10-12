using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Infrastructure.Payments;

public sealed class MockPaymentOptions
{
    public const string Section = "MockPayment";

    /// <summary>Simulated provider latency per call; 0 in integration tests (ISKELET §5.7.1).</summary>
    [Range(0, 60_000)]
    public int LatencyMs { get; set; } = 200;
}

/// <summary>
/// ISKELET §5.7.1: <c>tok_success</c> succeeds, <c>tok_declined</c> is declined with <c>card_declined</c>; refunds always
/// succeed. Call counts are exposed for tests (AC-10.3).
/// </summary>
public sealed class MockPaymentGateway(IOptions<MockPaymentOptions> options, TimeProvider clock) : IPaymentGateway
{
    public const string SuccessToken = "tok_success";
    public const string DeclinedToken = "tok_declined";
    public const string DeclinedFailureCode = "card_declined";

    private int _chargeCount;
    private int _refundCount;

    public static IReadOnlyList<string> SupportedTokens { get; } = [SuccessToken, DeclinedToken];

    public int ChargeCount => Volatile.Read(ref _chargeCount);

    public int RefundCount => Volatile.Read(ref _refundCount);

    public async Task<ChargeResult> ChargeAsync(decimal amount, string paymentToken, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chargeCount);
        await SimulateLatencyAsync(cancellationToken);
        return paymentToken switch
        {
            SuccessToken => ChargeResult.Success($"mock_{Guid.NewGuid()}"),
            DeclinedToken => ChargeResult.Declined(DeclinedFailureCode),
            _ => throw new ArgumentException("Unsupported mock payment token.", nameof(paymentToken)),
        };
    }

    public async Task RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _refundCount);
        await SimulateLatencyAsync(cancellationToken);
    }

    private Task SimulateLatencyAsync(CancellationToken cancellationToken) =>
        options.Value.LatencyMs > 0
            ? Task.Delay(TimeSpan.FromMilliseconds(options.Value.LatencyMs), clock, cancellationToken)
            : Task.CompletedTask;
}
