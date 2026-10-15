using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public sealed class MonumentPermit : TenantEntity
{
    private MonumentPermit()
    {
        HolderName = "";
    }

    public Guid CemeteryId { get; private set; }

    public Guid GraveSiteId { get; private set; }

    public string HolderName { get; private set; }

    public DateOnly IssuedOn { get; private set; }

    public DateOnly ExpiresOn { get; private set; }

    public static MonumentPermit Issue(
        Guid tenantId,
        Guid cemeteryId,
        Guid graveSiteId,
        string? holderName,
        DateOnly issuedOn,
        DateOnly expiresOn)
    {
        RequireParties(tenantId, cemeteryId, graveSiteId);
        if (issuedOn == default || expiresOn == default || expiresOn < issuedOn)
            throw new DomainRuleException("permit.date_invalid");

        return new MonumentPermit
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            GraveSiteId = graveSiteId,
            HolderName = OperationsText.Name(holderName, "permit.name_invalid"),
            IssuedOn = issuedOn,
            ExpiresOn = expiresOn,
        };
    }

    public bool Covers(Guid graveSiteId, DateOnly day) =>
        GraveSiteId == graveSiteId && IssuedOn <= day && day <= ExpiresOn;

    public string StatusOn(DateOnly today) => ExpiresOn < today ? "expired" : "active";

    public static MonumentPermit? BestCover(IEnumerable<MonumentPermit> permits, Guid graveSiteId, DateOnly day)
    {
        MonumentPermit? best = null;
        foreach (var permit in permits)
            best = Prefer(best, permit, graveSiteId, day);
        return best;
    }

    private static MonumentPermit? Prefer(MonumentPermit? best, MonumentPermit permit, Guid graveSiteId, DateOnly day)
    {
        if (!permit.Covers(graveSiteId, day))
            return best;
        if (best is null || permit.IssuedOn > best.IssuedOn)
            return permit;
        if (permit.IssuedOn == best.IssuedOn && permit.Id.CompareTo(best.Id) > 0)
            return permit;
        return best;
    }

    private static void RequireParties(Guid tenantId, Guid cemeteryId, Guid graveSiteId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("cemetery.not_found");
        if (graveSiteId == Guid.Empty)
            throw new DomainRuleException("permit.site_invalid");
    }
}
