namespace Cemetery.Domain.Identity;

public sealed class Membership : TenantEntity
{
    private Membership()
    {
    }

    public Guid UserId { get; private set; }

    public MembershipRole Role { get; private set; }

    public static Membership Create(Guid organizationId, Guid userId, MembershipRole role)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
            throw new DomainRuleException("membership.invalid");

        return new Membership
        {
            Id = Guid.CreateVersion7(),
            TenantId = organizationId,
            UserId = userId,
            Role = role,
        };
    }
}
