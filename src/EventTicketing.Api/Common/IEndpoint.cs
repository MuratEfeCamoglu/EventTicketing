namespace EventTicketing.Api.Common;

/// <summary>A use case's route mapping. <paramref name="app"/> is the <c>/api/v1</c> group.</summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
