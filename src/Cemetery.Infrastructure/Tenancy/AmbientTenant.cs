using Cemetery.Application.Abstractions;

namespace Cemetery.Infrastructure.Tenancy;

public sealed class AmbientTenant : ITenantContext
{
    public Guid? OrganizationId { get; private set; }

    public void Use(Guid organizationId) => OrganizationId = organizationId;
}

public sealed class AmbientSession : ISessionHints
{
    public string InvitationHash { get; set; } = "";
}
