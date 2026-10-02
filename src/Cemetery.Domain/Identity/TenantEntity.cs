namespace Cemetery.Domain.Identity;

public abstract class TenantEntity
{
    public Guid Id { get; protected set; }

    public Guid TenantId { get; protected set; }
}
