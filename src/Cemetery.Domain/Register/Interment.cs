using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public sealed class Interment : TenantEntity
{
    private Interment()
    {
    }

    public Guid CemeteryId { get; private set; }

    public Guid DeceasedId { get; private set; }

    public Guid GraveSiteId { get; private set; }

    public int Position { get; private set; }

    public IntermentKind Kind { get; private set; }

    public DateOnly BuriedOn { get; private set; }

    public DateOnly? ExhumedOn { get; private set; }

    public DateOnly? TransferredOn { get; private set; }

    public DateOnly? SupersededOn { get; private set; }

    public bool Occupies => ExhumedOn is null && TransferredOn is null && SupersededOn is null;

    public static Interment Record(
        Guid tenantId,
        Guid cemeteryId,
        Guid deceasedId,
        Guid graveSiteId,
        int position,
        IntermentKind kind,
        DateOnly buriedOn)
    {
        if (tenantId == Guid.Empty || cemeteryId == Guid.Empty || deceasedId == Guid.Empty || graveSiteId == Guid.Empty)
            throw new DomainRuleException("interment.location_invalid");

        return new Interment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            DeceasedId = deceasedId,
            GraveSiteId = graveSiteId,
            Position = position,
            Kind = kind,
            BuriedOn = buriedOn,
        };
    }

    public void Exhume(DateOnly on)
    {
        RequireActive(on);
        ExhumedOn = on;
    }

    public void MarkTransferred(DateOnly on)
    {
        RequireActive(on);
        TransferredOn = on;
    }

    public void Supersede(DateOnly on)
    {
        RequireActive(on);
        SupersededOn = on;
    }

    private void RequireActive(DateOnly on)
    {
        if (!Occupies)
            throw new DomainRuleException("interment.not_active");
        if (on < BuriedOn)
            throw new DomainRuleException("interment.date_invalid");
    }
}
