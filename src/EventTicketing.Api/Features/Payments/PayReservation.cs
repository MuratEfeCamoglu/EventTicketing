using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Payments;

/// <summary>
/// Mock payment and ticket issuing (ISKELET §5.7). The <c>Idempotency-Key</c> makes retries safe: a key that already has a
/// <c>payments</c> row returns that row's recorded outcome without charging again (AC-10.3). When the charge succeeds but
/// the confirmation loses a race (expiry, cancellation, a concurrent request), the charge is refunded and recorded as a
/// <c>Refunded</c> payment under the same key (AC-10.6).
/// </summary>
public static class PayReservation
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string UniqueIdempotencyKeyIndex = "ux_payments_idempotency_key";

    public sealed record Request(string PaymentToken);

    public sealed record Response(Guid PaymentId, PaymentStatus Status, IReadOnlyList<TicketDto> Tickets);

    public sealed record TicketDto(Guid TicketId, Guid EventSeatId, string Code, string SeatLabel);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.PaymentToken)
            .Must(token => MockPaymentGateway.SupportedTokens.Contains(token))
            .WithMessage($"Payment token must be one of: {string.Join(", ", MockPaymentGateway.SupportedTokens)}.");
    }

    public sealed class Handler(AppDbContext db, IPaymentGateway gateway, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<Response>> HandleAsync(Guid reservationId, Guid userId, Guid idempotencyKey, Request request, CancellationToken cancellationToken)
        {
            // Ownership first, so a replayed key never reveals someone else's payment.
            var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, cancellationToken);
            if (reservation is null)
            {
                return DomainErrors.NotFound;
            }

            if (await FindByKeyAsync(idempotencyKey, cancellationToken) is { } recorded)
            {
                return recorded.ReservationId == reservationId
                    ? await RecordedOutcomeAsync(recorded, cancellationToken)
                    : DomainErrors.Payments.IdempotencyKeyReused;
            }

            if (reservation.EnsurePayable(clock.GetUtcNow()) is { IsSuccess: false } notPayable)
            {
                return notPayable.Error;
            }

            var charge = await gateway.ChargeAsync(reservation.TotalAmount, request.PaymentToken, idempotencyKey, cancellationToken);
            if (!charge.Succeeded)
            {
                return await RecordDeclineAsync(reservation, idempotencyKey, charge, cancellationToken);
            }

            var confirmed = await TryConfirmAsync(reservation, idempotencyKey, charge, cancellationToken);
            return confirmed ?? await RefundAsync(reservation, idempotencyKey, charge, cancellationToken);
        }

        /// <summary>Step 5: payment, confirmation, sold seats and tickets in one transaction; null when the confirmation lost a race.</summary>
        private async Task<Response?> TryConfirmAsync(Reservation reservation, Guid idempotencyKey, ChargeResult charge, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var reservedSeatCount = await db.ReservationSeats.CountAsync(s => s.ReservationId == reservation.Id, cancellationToken);
            var seats = await db.EventSeats
                .Where(s => s.HoldReservationId == reservation.Id && s.Status == EventSeatStatus.Held)
                .ToListAsync(cancellationToken);
            if (seats.Count != reservedSeatCount || !reservation.Confirm(now).IsSuccess)
            {
                logger.LogWarning("Reservation {ReservationId} could not be confirmed after a successful charge", reservation.Id);
                return null;
            }

            var payment = new Payment(reservation.Id, idempotencyKey, reservation.TotalAmount, PaymentStatus.Succeeded, charge.ProviderReference, null, now);
            db.Payments.Add(payment);
            foreach (var seat in seats)
            {
                seat.Sell(reservation.Id);
                db.Tickets.Add(new Ticket(reservation.Id, seat.Id, reservation.UserId, Ticket.GenerateCode(), now));
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || exception.IsUniqueViolation())
            {
                // xmin conflict (expired, cancelled, paid concurrently) or a unique index (same key, second ticket for a seat).
                logger.LogWarning("Confirmation of reservation {ReservationId} lost a concurrency race", reservation.Id);
                return null;
            }

            logger.LogInformation("Reservation {ReservationId} paid with payment {PaymentId}; {TicketCount} tickets issued", reservation.Id, payment.Id, seats.Count);
            return new Response(payment.Id, payment.Status, await TicketsOfAsync(reservation.Id, cancellationToken));
        }

        /// <summary>Step 5, failure branch: refund, then record the refund under the request's key unless a concurrent request with that key won.</summary>
        private async Task<Result<Response>> RefundAsync(Reservation reservation, Guid idempotencyKey, ChargeResult charge, CancellationToken cancellationToken)
        {
            await gateway.RefundAsync(charge.ProviderReference!, reservation.TotalAmount, cancellationToken);
            db.ChangeTracker.Clear();

            if (await FindByKeyAsync(idempotencyKey, cancellationToken) is { } winner)
            {
                return await RecordedOutcomeAsync(winner, cancellationToken);
            }

            var now = clock.GetUtcNow();
            db.Payments.Add(new Payment(reservation.Id, idempotencyKey, reservation.TotalAmount, PaymentStatus.Refunded, charge.ProviderReference, null, now));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation(UniqueIdempotencyKeyIndex))
            {
                db.ChangeTracker.Clear();
                return await RecordedOutcomeAsync((await FindByKeyAsync(idempotencyKey, cancellationToken))!, cancellationToken);
            }

            logger.LogInformation("Charge for reservation {ReservationId} refunded", reservation.Id);
            var current = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id, cancellationToken);
            return current.StatusAt(now) == ReservationStatus.Expired ? DomainErrors.Reservations.Expired : DomainErrors.Reservations.InvalidState;
        }

        /// <summary>
        /// Step 6: a Failed payment row; the reservation and its seats change only if still Pending/Held, so a concurrent
        /// expiry does not turn into a conflict.
        /// </summary>
        private async Task<Result<Response>> RecordDeclineAsync(Reservation reservation, Guid idempotencyKey, ChargeResult charge, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            var recorded = await TryRecordDeclineAsync(reservation.Id, idempotencyKey, reservation.TotalAmount, charge.FailureCode, now, cancellationToken);
            if (!recorded)
            {
                db.ChangeTracker.Clear();
                return await RecordedOutcomeAsync((await FindByKeyAsync(idempotencyKey, cancellationToken))!, cancellationToken);
            }

            logger.LogInformation("Payment for reservation {ReservationId} declined with {FailureCode}", reservation.Id, charge.FailureCode);
            return DomainErrors.Payments.Declined;
        }

        private async Task<bool> TryRecordDeclineAsync(Guid reservationId, Guid idempotencyKey, decimal amount, string? failureCode, DateTimeOffset now, CancellationToken cancellationToken)
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Payments.Add(new Payment(reservationId, idempotencyKey, amount, PaymentStatus.Failed, null, failureCode, now));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation(UniqueIdempotencyKeyIndex))
            {
                return false;
            }

            await db.Reservations
                .Where(r => r.Id == reservationId && r.Status == ReservationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.PaymentFailed).SetProperty(r => r.UpdatedAt, now), cancellationToken);
            await db.EventSeats
                .Where(s => s.HoldReservationId == reservationId && s.Status == EventSeatStatus.Held)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Status, EventSeatStatus.Available).SetProperty(x => x.HoldReservationId, (Guid?)null).SetProperty(x => x.HeldUntil, (DateTimeOffset?)null),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        private Task<Payment?> FindByKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
            db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

        /// <summary>AC-10.3 mapping: Succeeded → 200 + tickets, Failed → 402, Refunded → 409 reservation.expired.</summary>
        private async Task<Result<Response>> RecordedOutcomeAsync(Payment payment, CancellationToken cancellationToken) => payment.Status switch
        {
            PaymentStatus.Succeeded => new Response(payment.Id, payment.Status, await TicketsOfAsync(payment.ReservationId, cancellationToken)),
            PaymentStatus.Failed => DomainErrors.Payments.Declined,
            _ => DomainErrors.Reservations.Expired,
        };

        private Task<List<TicketDto>> TicketsOfAsync(Guid reservationId, CancellationToken cancellationToken) => (
                from t in db.Tickets.AsNoTracking()
                join es in db.EventSeats on t.EventSeatId equals es.Id
                join s in db.Seats on es.SeatId equals s.Id
                where t.ReservationId == reservationId
                orderby s.Section, s.Row, s.Number
                select new TicketDto(t.Id, t.EventSeatId, t.Code, s.Section + "-" + s.Row + "-" + s.Number.ToString()))
            .ToListAsync(cancellationToken);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/reservations/{id:guid}/payments", async (
                    Guid id,
                    [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
                    Request request,
                    Handler handler,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                {
                    if (!Guid.TryParseExact(idempotencyKey, "D", out var key))
                    {
                        return DomainErrors.Validation("idempotencyKey", $"The {IdempotencyKeyHeader} header must be a UUID.").ToProblem();
                    }

                    return (await handler.HandleAsync(id, context.User.GetUserId(), key, request, cancellationToken)).ToHttpResult(TypedResults.Ok);
                })
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Customer)
                .WithTags("Payments");
    }
}
