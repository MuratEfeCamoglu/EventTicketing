using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Email;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.Api.Infrastructure.Persistence;
using EventTicketing.Api.Infrastructure.Qr;
using EventTicketing.Contracts;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Payments;

/// <summary>
/// Mock payment and ticket issuing (ISKELET §5.7). The <c>Idempotency-Key</c> makes retries safe: a key that already has a
/// <c>payments</c> row returns that row's recorded outcome without charging again (AC-10.3). When the charge succeeds but
/// the confirmation loses a race (expiry, cancellation, a concurrent request), the charge is refunded and recorded as a
/// <c>Refunded</c> payment under the same key (AC-10.6). (v3) <see cref="TicketPurchased"/> is published through the EF outbox in
/// the confirmation transaction (AC-17.1).
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

    public sealed class Handler(
        AppDbContext db,
        IPaymentGateway gateway,
        IPublishEndpoint publisher,
        IEmailSender email,
        EventCacheInvalidator cache,
        TimeProvider clock,
        ILogger<Handler> logger)
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
            var confirmed = charge.Succeeded ? await TryConfirmAsync(reservation, idempotencyKey, charge, cancellationToken) : null;
            var result = confirmed is not null ? confirmed
                : charge.Succeeded ? await RefundAsync(reservation, idempotencyKey, charge, cancellationToken)
                : await RecordDeclineAsync(reservation, idempotencyKey, charge, cancellationToken);

            // Every path after a charge may have sold or released seats (ISKELET §5.9).
            await cache.SeatsChangedAsync(reservation.EventId, cancellationToken);
            if (confirmed is not null)
            {
                await SendTicketsAsync(reservation.Id, reservation.UserId, confirmed.Tickets, cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Best effort after the commit (ISKELET §5.7, F-12): a failure is logged and the payment response stays 200
        /// (AC-12.2). An e-mail lost here is accepted in the MVP (R-11); V3-2 moves it behind the outbox.
        /// </summary>
        private async Task SendTicketsAsync(Guid reservationId, Guid userId, IReadOnlyList<TicketDto> tickets, CancellationToken cancellationToken)
        {
            try
            {
                var details = await (
                        from r in db.Reservations.AsNoTracking()
                        join e in db.Events on r.EventId equals e.Id
                        join h in db.Halls on e.HallId equals h.Id
                        join u in db.Users on r.UserId equals u.Id
                        where r.Id == reservationId
                        select new { e.Title, e.StartsAt, HallName = h.Name, u.Email, u.FullName })
                    .SingleAsync(cancellationToken);

                var lines = tickets.Select(t => $"- {t.SeatLabel}: {t.Code}");
                var message = new EmailMessage(
                    details.Email,
                    details.FullName,
                    $"Your tickets for {details.Title}",
                    $"Hello {details.FullName},{Environment.NewLine}{Environment.NewLine}"
                        + $"{details.Title} · {details.StartsAt:u} · {details.HallName}{Environment.NewLine}"
                        + string.Join(Environment.NewLine, lines),
                    tickets.Select(t => new EmailAttachment($"ticket-{t.SeatLabel}.png", "image/png", QrCodeGenerator.GeneratePng(Ticket.QrContent(t.Code)))).ToList());

                await email.SendAsync(message, cancellationToken);
                logger.LogInformation("Ticket e-mail for reservation {ReservationId} sent to {Email}", reservationId, LogMasking.MaskEmail(details.Email));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ticket e-mail for reservation {ReservationId} of user {UserId} could not be sent", reservationId, userId);
            }
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
            var issued = new List<(Ticket Ticket, Guid SeatId)>(seats.Count);
            foreach (var seat in seats)
            {
                seat.Sell(reservation.Id);
                var ticket = new Ticket(reservation.Id, seat.Id, reservation.UserId, Ticket.GenerateCode(), now);
                db.Tickets.Add(ticket);
                issued.Add((ticket, seat.SeatId));
            }

            // The outbox publish endpoint adds an outbox_message row to this context: it commits or rolls back with the payment.
            await publisher.Publish(await TicketPurchasedAsync(reservation, issued, now, cancellationToken), cancellationToken);

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

        private async Task<TicketPurchased> TicketPurchasedAsync(
            Reservation reservation, IReadOnlyList<(Ticket Ticket, Guid SeatId)> issued, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var details = await (
                    from e in db.Events.AsNoTracking()
                    join h in db.Halls on e.HallId equals h.Id
                    from u in db.Users
                    where e.Id == reservation.EventId && u.Id == reservation.UserId
                    select new { e.Title, e.StartsAt, HallName = h.Name, u.Email, u.FullName })
                .SingleAsync(cancellationToken);

            var seatIds = issued.Select(i => i.SeatId).ToList();
            var seats = await db.Seats.AsNoTracking()
                .Where(s => seatIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Section, s.Row, s.Number })
                .ToDictionaryAsync(s => s.Id, cancellationToken);

            var tickets = issued
                .Select(i => (i.Ticket, Seat: seats[i.SeatId]))
                .OrderBy(t => t.Seat.Section).ThenBy(t => t.Seat.Row).ThenBy(t => t.Seat.Number)
                .Select(t => new PurchasedTicket(t.Ticket.Id, $"{t.Seat.Section}-{t.Seat.Row}-{t.Seat.Number}", t.Ticket.Code))
                .ToList();

            return new TicketPurchased(
                reservation.Id, reservation.UserId, details.Email, details.FullName, details.Title, details.StartsAt, details.HallName, tickets, now);
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
