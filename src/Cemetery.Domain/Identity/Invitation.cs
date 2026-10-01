namespace Cemetery.Domain.Identity;

public sealed class Invitation : TenantEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
        Email = default;
        TokenHash = "";
    }

    public EmailAddress Email { get; private set; }

    public MembershipRole Role { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public static Invitation Issue(
        Guid organizationId,
        EmailAddress email,
        MembershipRole role,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash) || expiresAt <= now)
            throw new DomainRuleException("invitation.invalid");

        return new Invitation
        {
            Id = Guid.CreateVersion7(),
            TenantId = organizationId,
            Email = email,
            Role = role,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    public void Accept(EmailAddress email, DateTimeOffset now)
    {
        if (AcceptedAt is not null)
            throw new DomainRuleException("invitation.already_accepted");

        if (now >= ExpiresAt)
            throw new DomainRuleException("invitation.expired");

        if (Email != email)
            throw new DomainRuleException("invitation.email_mismatch");

        AcceptedAt = now;
    }
}
