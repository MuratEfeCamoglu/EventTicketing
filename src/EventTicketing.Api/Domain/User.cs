using EventTicketing.Api.Common;

namespace EventTicketing.Api.Domain;

public sealed class User
{
    private User()
    {
    }

    public User(string email, string fullName, UserRole role, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        Email = NormalizeEmail(email);
        FullName = fullName;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Stored normalized (ISKELET §5.11), so uniqueness is case-insensitive.</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>Customer ↔ Organizer only; the Admin role is neither granted nor taken away through the API (AC-03.4).</summary>
    public Result ChangeRole(UserRole role)
    {
        if (role == UserRole.Admin || Role == UserRole.Admin)
        {
            return DomainErrors.Users.CannotAssignAdmin;
        }

        Role = role;
        return Result.Success();
    }
}
