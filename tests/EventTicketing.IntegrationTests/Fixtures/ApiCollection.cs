namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>All integration tests share one host and one database and run sequentially (ISKELET §3.2).</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
