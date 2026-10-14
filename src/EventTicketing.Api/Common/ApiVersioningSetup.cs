using System.Text.RegularExpressions;
using Asp.Versioning;
using EventTicketing.Api.Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EventTicketing.Api.Common;

/// <summary>URL segment API versioning (bonus AC-20.2) with Asp.Versioning.Http.</summary>
public static partial class ApiVersioningSetup
{
    public static readonly ApiVersion CurrentVersion = new(1, 0);

    private const string SupportedVersionsHeader = "api-supported-versions";
    private const string VersionRouteParameter = "version";

    public static IServiceCollection AddEventTicketingApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = CurrentVersion;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        return services;
    }

    /// <summary>
    /// Answers <c>/api/v{x}/...</c> with <c>400</c> ProblemDetails when <c>x</c> is not a supported version. Asp.Versioning
    /// itself always answers 404 for an unknown URL segment version, because the version is part of the path.
    /// </summary>
    public static WebApplication UseUnsupportedApiVersionResponse(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var match = VersionSegment().Match(context.Request.Path.Value ?? string.Empty);
            if (match.Success
                && !(ApiVersionParser.Default.TryParse(match.Groups["version"].Value, out var requested) && requested == CurrentVersion))
            {
                context.Response.Headers[SupportedVersionsHeader] = CurrentVersion.ToString();
                await DomainErrors.Validation(VersionRouteParameter, $"API version '{match.Groups["version"].Value}' is not supported; supported: {CurrentVersion}.")
                    .ToProblem()
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }

    /// <summary>The OpenAPI document shows concrete <c>/api/v1/...</c> paths instead of a <c>{version}</c> parameter.</summary>
    public static OpenApiOptions UseConcreteVersionPaths(this OpenApiOptions options) =>
        options.AddDocumentTransformer((document, _, _) =>
        {
            var segment = $"v{{{VersionRouteParameter}}}";
            var concrete = $"v{CurrentVersion.MajorVersion}";
            var paths = new OpenApiPaths();
            foreach (var (path, item) in document.Paths)
            {
                foreach (var operation in item.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
                {
                    var version = operation.Parameters?.FirstOrDefault(p => p.Name == VersionRouteParameter && p.In == ParameterLocation.Path);
                    if (version is not null)
                    {
                        operation.Parameters!.Remove(version);
                    }
                }

                paths[path.Replace(segment, concrete, StringComparison.Ordinal)] = item;
            }

            document.Paths = paths;
            return Task.CompletedTask;
        });

    [GeneratedRegex(@"^/api/v(?<version>[^/]+)(/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionSegment();
}
