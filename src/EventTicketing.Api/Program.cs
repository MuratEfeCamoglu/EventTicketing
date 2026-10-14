using System.Text.Json.Serialization;
using EventTicketing.Api.Common;
using EventTicketing.Api.Features.Reservations;
using EventTicketing.Api.Infrastructure.Auth;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Email;
using EventTicketing.Api.Infrastructure.Locking;
using EventTicketing.Api.Infrastructure.Messaging;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogging();
builder.AddPersistence();
builder.AddCaching();
builder.AddSeatLocking();
builder.AddAuth();
builder.AddPayments();
builder.AddEmail();
builder.AddMessaging();

builder.Services.AddOptions<ReservationOptions>()
    .BindConfiguration(ReservationOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EventTicketingMetrics>();
builder.Services.AddHostedService<ReservationExpiryService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsSetup.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFeatureSlices(typeof(Program).Assembly);

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapFeatureEndpoints();

await app.RunAsync();

public partial class Program;
