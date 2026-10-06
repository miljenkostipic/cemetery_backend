using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public sealed class DeceasedRevision : TenantEntity
{
    private DeceasedRevision()
    {
        GivenName = "";
        FamilyName = "";
        Reason = "";
    }

    public Guid DeceasedId { get; private set; }

    public int Version { get; private set; }

    public string GivenName { get; private set; }

    public string FamilyName { get; private set; }

    public DateOnly? BornOn { get; private set; }

    public DateOnly? DiedOn { get; private set; }

    public string Reason { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static DeceasedRevision Write(Deceased deceased, string reason, DateTimeOffset at)
    {
        return new DeceasedRevision
        {
            Id = Guid.CreateVersion7(),
            TenantId = deceased.TenantId,
            DeceasedId = deceased.Id,
            Version = deceased.Version,
            GivenName = deceased.GivenName,
            FamilyName = deceased.FamilyName,
            BornOn = deceased.BornOn,
            DiedOn = deceased.DiedOn,
            Reason = reason,
            At = at,
        };
    }
}
