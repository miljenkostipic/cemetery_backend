using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public sealed class Deceased : TenantEntity
{
    private Deceased()
    {
        GivenName = "";
        FamilyName = "";
    }

    public Guid CemeteryId { get; private set; }

    public string GivenName { get; private set; }

    public string FamilyName { get; private set; }

    public DateOnly? BornOn { get; private set; }

    public DateOnly? DiedOn { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public string? Epitaph { get; private set; }

    public string? PhotoUrl { get; private set; }

    public static Deceased Record(
        Guid tenantId,
        Guid cemeteryId,
        string? givenName,
        string? familyName,
        DateOnly? bornOn,
        DateOnly? diedOn,
        DateTimeOffset recordedAt)
    {
        if (tenantId == Guid.Empty || cemeteryId == Guid.Empty)
            throw new DomainRuleException("deceased.location_required");

        var name = PersonName.Create(givenName, familyName);
        LifeDates.RequireOrder(bornOn, diedOn);
        return new Deceased
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            GivenName = name.Given,
            FamilyName = name.Family,
            BornOn = bornOn,
            DiedOn = diedOn,
            Version = 1,
            RecordedAt = recordedAt,
        };
    }

    public DeceasedRevision Correct(
        string? givenName,
        string? familyName,
        DateOnly? bornOn,
        DateOnly? diedOn,
        string? reason,
        DateTimeOffset at)
    {
        var explanation = CorrectionReason.Require(reason);
        var name = PersonName.Create(givenName, familyName);
        LifeDates.RequireOrder(bornOn, diedOn);
        GivenName = name.Given;
        FamilyName = name.Family;
        BornOn = bornOn;
        DiedOn = diedOn;
        Version++;
        return DeceasedRevision.Write(this, explanation, at);
    }

    public void Remove(DateTimeOffset at)
    {
        if (RemovedAt is not null)
            throw new DomainRuleException("deceased.removed");
        RemovedAt = at;
    }

    public void DescribeMemorial(string? epitaph, string? photoUrl)
    {
        Epitaph = PublicListing.EpitaphOf(epitaph);
        PhotoUrl = PublicListing.PhotoOf(photoUrl);
    }
}
