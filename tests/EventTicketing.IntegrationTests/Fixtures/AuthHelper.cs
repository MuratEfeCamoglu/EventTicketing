using System.Net.Http.Headers;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>Authenticated client helpers. Token acquisition (register + login) is added with the auth endpoints in M3.</summary>
public static class AuthHelper
{
    public static HttpClient WithBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
