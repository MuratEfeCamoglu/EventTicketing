namespace EventTicketing.Api.Infrastructure.Email;

public static class EmailSetup
{
    public static WebApplicationBuilder AddEmail(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return builder;
    }
}
