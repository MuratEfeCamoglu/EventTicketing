using EventTicketing.Api.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.UnitTests.Common;

public static class SampleSlice
{
    public sealed class Handler
    {
        public string Handle() => "handled";
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) => app.MapGet("/sample", (Handler handler) => handler.Handle());
    }
}

public sealed class EndpointRegistrationTests
{
    [Fact]
    public void AddFeatureSlices_Assembly_RegistersEndpointsOnceAndHandlersScoped()
    {
        var services = new ServiceCollection();

        services.AddFeatureSlices(typeof(EndpointRegistrationTests).Assembly);
        services.AddFeatureSlices(typeof(EndpointRegistrationTests).Assembly);

        services.Where(d => d.ServiceType == typeof(IEndpoint) && d.ImplementationType == typeof(SampleSlice.Endpoint)).ShouldHaveSingleItem()
            .Lifetime.ShouldBe(ServiceLifetime.Singleton);
        services.Where(d => d.ServiceType == typeof(SampleSlice.Handler)).ShouldHaveSingleItem()
            .Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void MapFeatureEndpoints_DiscoveredEndpoint_IsMappedUnderApiV1()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFeatureSlices(typeof(EndpointRegistrationTests).Assembly);
        var app = builder.Build();

        app.MapFeatureEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText);
        routes.ShouldContain("/api/v1/sample");
    }
}
