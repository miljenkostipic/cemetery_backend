using Cemetery.Domain.Identity;

namespace Cemetery.Application.Abstractions;

public interface ITenantContext
{
    Guid? OrganizationId { get; }

    void Use(Guid organizationId);
}

public interface ISessionHints
{
    string InvitationHash { get; set; }

    bool PublicRead { get; set; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IActiveOrganizationStore
{
    void Remember(Guid organizationId);
}

public interface IUserAccountGateway
{
    Task<Guid> CreateAsync(EmailAddress email, string password, string displayName, CancellationToken cancellationToken);

    Task<UserAccount?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken);
}

public enum PasswordSignInStatus
{
    Succeeded,
    Failed,
    LockedOut,
}

public interface IPasswordSignIn
{
    Task<PasswordSignInStatus> SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken);
}

public enum PasswordResetStatus
{
    Succeeded,
    Failed,
}

public interface IPasswordReset
{
    Task<string?> CreateTokenAsync(EmailAddress email, CancellationToken cancellationToken);

    Task<PasswordResetStatus> ResetAsync(EmailAddress email, string token, string password, CancellationToken cancellationToken);
}

public interface IInvitationTokenFactory
{
    string Create();

    string Hash(string token);
}

public interface IEmailSender
{
    Task SendInvitationAsync(EmailAddress recipient, string acceptUrl, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(EmailAddress recipient, string resetUrl, CancellationToken cancellationToken);
}

public interface IAppLinks
{
    string InvitationUrl(string token);

    string PasswordResetUrl(string email, string token);
}

public sealed record UserAccount(Guid Id, EmailAddress Email, string DisplayName);

public sealed record OrganizationMembership(Guid OrganizationId, string Name, string Slug, MembershipRole Role);
