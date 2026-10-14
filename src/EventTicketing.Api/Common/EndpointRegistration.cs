using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventTicketing.Api.Common;

/// <summary>Reflection-based discovery of slices (ISKELET §5.3 rule 1): every <see cref="IEndpoint"/> and every nested <c>Handler</c>.</summary>
public static class EndpointRegistration
{
    /// <summary>Prefix of the current version, for <c>Location</c> headers.</summary>
    public const string ApiPrefix = "/api/v1";

    /// <summary>URL segment versioning (bonus AC-20.2); only 1.0 exists, any other segment is answered with 400.</summary>
    public const string VersionedPrefix = "/api/v{version:apiVersion}";
    private const string HandlerTypeName = "Handler";

    public static IServiceCollection AddFeatureSlices(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.DefinedTypes.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });
        foreach (var type in types)
        {
            if (type.IsAssignableTo(typeof(IEndpoint)))
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEndpoint), type));
            }
            else if (type.IsNested && type.Name == HandlerTypeName)
            {
                services.TryAddScoped(type);
            }
        }

        return services;
    }

    public static RouteGroupBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        var versions = app.NewApiVersionSet().HasApiVersion(ApiVersioningSetup.CurrentVersion).ReportApiVersions().Build();
        var group = app.MapGroup(VersionedPrefix).WithApiVersionSet(versions);
        foreach (var endpoint in app.ServiceProvider.GetServices<IEndpoint>())
        {
            endpoint.Map(group);
        }

        return group;
    }
}
