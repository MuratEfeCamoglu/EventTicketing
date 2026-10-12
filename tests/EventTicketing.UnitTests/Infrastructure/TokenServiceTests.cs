using System.Buffers.Text;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EventTicketing.UnitTests.Infrastructure;

public sealed class TokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly TokenService _service = new(
        Options.Create(new JwtOptions { SigningKey = new string('k', 48) }),
        new FakeTimeProvider(Now));

    [Fact]
    public void Issue_User_AccessTokenUsesClockAndCarriesIdentityClaims()
    {
        var user = new User("ayse@example.com", "Ayşe", UserRole.Organizer, Now);
        var familyId = Guid.CreateVersion7();

        var issued = _service.Issue(user, familyId);

        var jwt = new JsonWebToken(issued.AccessToken);
        jwt.Alg.ShouldBe("HS256");
        jwt.Issuer.ShouldBe("event-ticketing");
        jwt.Audiences.ShouldBe(["event-ticketing"]);
        jwt.IssuedAt.ShouldBe(Now.UtcDateTime);
        jwt.ValidFrom.ShouldBe(Now.UtcDateTime);
        jwt.ValidTo.ShouldBe(Now.AddMinutes(15).UtcDateTime);
        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.GetClaim("role").Value.ShouldBe("Organizer");
        jwt.GetClaim("email").Value.ShouldBe("ayse@example.com");
        issued.AccessTokenExpiresAt.ShouldBe(Now.AddMinutes(15));
    }

    [Fact]
    public void Issue_User_RefreshTokenIs64RandomBytesStoredAsHash()
    {
        var user = new User("ayse@example.com", "Ayşe", UserRole.Customer, Now);
        var familyId = Guid.CreateVersion7();

        var first = _service.Issue(user, familyId);
        var second = _service.Issue(user, familyId);

        Base64Url.DecodeFromChars(first.RefreshToken).Length.ShouldBe(64);
        first.RefreshToken.ShouldNotBe(second.RefreshToken);
        first.RefreshTokenEntity.TokenHash.ShouldBe(TokenService.Hash(first.RefreshToken));
        first.RefreshTokenEntity.TokenHash.ShouldMatch("^[0-9a-f]{64}$");
        first.RefreshTokenEntity.FamilyId.ShouldBe(familyId);
        first.RefreshTokenEntity.UserId.ShouldBe(user.Id);
        first.RefreshTokenEntity.ExpiresAt.ShouldBe(Now.AddDays(7));
    }
}
