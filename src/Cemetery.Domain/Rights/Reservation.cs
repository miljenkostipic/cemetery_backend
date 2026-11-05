using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Rights;

public sealed class Reservation : TenantEntity
{
    private Reservation()
    {
    }

    public Guid GraveSiteId { get; private set; }

    public Guid HolderId { get; private set; }

    public DateOnly Until { get; private set; }

    public bool Active { get; private set; }

    public static Reservation Hold(Guid tenantId, Guid graveSiteId, Guid holderId, DateOnly until, DateOnly today, GraveSiteStatus status)
    {
        if (tenantId == Guid.Empty || graveSiteId == Guid.Empty || holderId == Guid.Empty)
            throw new DomainRuleException("reservation.invalid");
        if (status != GraveSiteStatus.Available)
            throw new DomainRuleException("reservation.unavailable");
        if (until <= today)
            throw new DomainRuleException("right.date_invalid");

        return new Reservation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            GraveSiteId = graveSiteId,
            HolderId = holderId,
            Until = until,
            Active = true,
        };
    }

    public void Cancel()
    {
        if (!Active)
            throw new DomainRuleException("reservation.ended");

        Active = false;
    }
}

public static class BurialGuard
{
    public static void RefuseReserved(bool reserved)
    {
        if (reserved)
            throw new DomainRuleException("reservation.blocks_interment");
    }
}
