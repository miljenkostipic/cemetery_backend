using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Rights;

public enum GraveRightStatus
{
    Active = 1,
    Ended = 2,
}

public sealed class RightShare
{
    private RightShare()
    {
    }

    public Guid HolderId { get; private set; }

    public Guid TenantId { get; private set; }

    public int Percent { get; private set; }

    public decimal Fraction => Percent / 100m;

    public static RightShare Of(Guid holderId, int percent)
    {
        if (holderId == Guid.Empty || percent is < 1 or > 100)
            throw new DomainRuleException("right.share_invalid");

        return new RightShare { HolderId = holderId, Percent = percent };
    }

    internal void Stamp(Guid tenantId) => TenantId = tenantId;
}

public static class RightShares
{
    public static IReadOnlyList<RightShare> Require(IReadOnlyList<RightShare> shares)
    {
        if (shares.Count == 0 || shares.Sum(share => share.Fraction) != 1m)
            throw new DomainRuleException("right.share_invalid");

        var seen = new HashSet<Guid>();
        foreach (var share in shares)
        {
            if (!seen.Add(share.HolderId))
                throw new DomainRuleException("right.share_invalid");
        }

        return shares;
    }
}

public sealed class GraveRight : TenantEntity
{
    public const int MaxTermYears = 99;

    private readonly List<RightShare> shares = [];

    private GraveRight()
    {
    }

    public Guid GraveSiteId { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly? ValidTo { get; private set; }

    public GraveRightStatus Status { get; private set; }

    public bool Perpetual => ValidTo is null;

    public IReadOnlyList<RightShare> Shares => shares;

    public static GraveRight Grant(Guid tenantId, Guid graveSiteId, DateOnly from, int? termYears, bool perpetual, IReadOnlyList<RightShare> holders, GraveRight? current)
    {
        if (current is { Status: GraveRightStatus.Active })
            throw new DomainRuleException("right.overlap");

        return Create(tenantId, graveSiteId, from, End(from, termYears, perpetual), RightShares.Require(holders));
    }

    public void Renew(int years)
    {
        if (Status != GraveRightStatus.Active)
            throw new DomainRuleException("right.ended");
        if (Perpetual || years is < 1 or > MaxTermYears)
            throw new DomainRuleException(Perpetual ? "right.perpetual" : "right.term_invalid");

        ValidTo = ValidTo!.Value.AddYears(years);
    }

    public GraveRight Convey(DateOnly on, IReadOnlyList<RightShare> holders)
    {
        var nextEnd = NextEnd(ValidTo, on);
        EndOn(on);
        return Create(TenantId, GraveSiteId, on, nextEnd, RightShares.Require(holders));
    }

    private static DateOnly? NextEnd(DateOnly? previous, DateOnly on)
    {
        if (previous is null)
            return null;
        if (previous.Value <= on)
            throw new DomainRuleException("right.date_invalid");

        return previous;
    }

    public GraveRight Inherit(DateOnly on, IReadOnlyList<RightShare> holders) => Convey(on, holders);

    private void EndOn(DateOnly on)
    {
        if (Status != GraveRightStatus.Active || on < ValidFrom)
            throw new DomainRuleException(Status == GraveRightStatus.Active ? "right.date_invalid" : "right.ended");

        Status = GraveRightStatus.Ended;
        if (ValidTo is null || ValidTo > on)
            ValidTo = on;
    }

    private static DateOnly? End(DateOnly from, int? termYears, bool perpetual)
    {
        if (perpetual)
            return null;
        if (termYears is null or < 1 or > MaxTermYears)
            throw new DomainRuleException("right.term_invalid");

        return from.AddYears(termYears.Value);
    }

    private static GraveRight Create(Guid tenantId, Guid graveSiteId, DateOnly from, DateOnly? to, IReadOnlyList<RightShare> holders)
    {
        if (tenantId == Guid.Empty || graveSiteId == Guid.Empty || from == default)
            throw new DomainRuleException("right.invalid");

        var right = new GraveRight
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            GraveSiteId = graveSiteId,
            ValidFrom = from,
            ValidTo = to,
            Status = GraveRightStatus.Active,
        };
        foreach (var share in holders)
            share.Stamp(tenantId);
        right.shares.AddRange(holders);
        return right;
    }
}
