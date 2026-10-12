namespace EventTicketing.Api.Infrastructure.Payments;

public static class PaymentSetup
{
    public static WebApplicationBuilder AddPayments(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<MockPaymentOptions>()
            .BindConfiguration(MockPaymentOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // One instance behind both types, so tests can read the call counters of the gateway the API uses.
        builder.Services.AddSingleton<MockPaymentGateway>();
        builder.Services.AddSingleton<IPaymentGateway>(services => services.GetRequiredService<MockPaymentGateway>());
        return builder;
    }
}
