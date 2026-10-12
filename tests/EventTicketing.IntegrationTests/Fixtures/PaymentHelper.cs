using System.Net.Http.Json;
using EventTicketing.Api.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Fixtures;

public static class PaymentHelper
{
    public static Task<HttpResponseMessage> PayAsync(this HttpClient client, Guid reservationId, string paymentToken = MockPaymentGateway.SuccessToken, Guid? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{reservationId}/payments")
        {
            Content = JsonContent.Create(new { paymentToken }),
        };
        request.Headers.Add("Idempotency-Key", (idempotencyKey ?? Guid.NewGuid()).ToString());
        return client.SendAsync(request);
    }

    /// <summary>Holds the seats, pays with <c>tok_success</c> and returns the reservation id; the client must be a Customer.</summary>
    public static async Task<Guid> BuyAsync(this HttpClient client, Guid eventId, params Guid[] eventSeatIds)
    {
        var reservationId = await client.HoldOrFailAsync(eventId, eventSeatIds);
        (await client.PayAsync(reservationId)).EnsureSuccessStatusCode();
        return reservationId;
    }

    public static MockPaymentGateway PaymentGateway(this ApiFactory factory) => factory.Services.GetRequiredService<MockPaymentGateway>();
}
