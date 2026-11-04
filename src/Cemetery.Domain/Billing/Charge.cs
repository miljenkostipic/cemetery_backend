using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Billing;

public static class BillingYear
{
    public const int First = 2000;

    public const int Last = 2100;

    public const int DueMonth = 3;

    public const int DueDay = 31;

    public const string Invalid = "charge.year_invalid";

    public static int Require(int year)
    {
        if (year < First || year > Last)
            throw new DomainRuleException(Invalid);

        return year;
    }

    public static bool Accepts(int year) => RuleCheck.Accepts(() => Require(year));

    public static DateOnly DueOn(int year) => new(year, DueMonth, DueDay);
}

public enum ChargeStatus
{
    Open = 1,
    Reminded = 2,
    Paid = 3,
}

public static class ChargeStatuses
{
    public const int MaxLength = 16;

    public static bool IsArrears(ChargeStatus status) => status is ChargeStatus.Open or ChargeStatus.Reminded;
}

public readonly record struct ChargeKey(Guid GraveSiteId, Guid FeeScheduleId);

public sealed class Charge : TenantEntity
{
    public const int HolderMaxLength = 200;

    private Charge()
    {
        GraveSiteCode = "";
        HolderName = "";
        Description = "";
        Currency = "";
    }

    public Guid CemeteryId { get; private set; }

    public Guid GraveSiteId { get; private set; }

    public Guid FeeScheduleId { get; private set; }

    public string GraveSiteCode { get; private set; }

    public string HolderName { get; private set; }

    public FeeKind Kind { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public ChargeStatus Status { get; private set; }

    public DateOnly DueOn { get; private set; }

    public int Year { get; private set; }

    public Guid? LatestPaymentId { get; private set; }

    public static Charge Open(Guid tenantId, GraveSite site, FeeSchedule fee, int year)
    {
        RequirePlace(tenantId, site, fee);
        return Create(tenantId, site, fee, BillingYear.Require(year));
    }

    private static void RequirePlace(Guid tenantId, GraveSite site, FeeSchedule fee)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (site.TenantId != tenantId || fee.TenantId != tenantId)
            throw new DomainRuleException("tenant.required");
        if (site.CemeteryId != fee.CemeteryId)
            throw new DomainRuleException("cemetery.not_found");
    }

    private static Charge Create(Guid tenantId, GraveSite site, FeeSchedule fee, int year) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = site.CemeteryId,
            GraveSiteId = site.Id,
            FeeScheduleId = fee.Id,
            GraveSiteCode = site.Code,
            HolderName = "",
            Kind = fee.Kind,
            Description = fee.Name,
            Amount = fee.Amount,
            Currency = fee.Currency,
            Status = ChargeStatus.Open,
            DueOn = BillingYear.DueOn(year),
            Year = year,
        };

    public bool Remind()
    {
        if (Status != ChargeStatus.Open)
            return false;

        Status = ChargeStatus.Reminded;
        return true;
    }

    public void Pay(Guid paymentId)
    {
        if (paymentId == Guid.Empty)
            throw new DomainRuleException("payment.not_found");
        if (Status is not (ChargeStatus.Open or ChargeStatus.Reminded))
            throw new DomainRuleException("charge.already_paid");

        Status = ChargeStatus.Paid;
        LatestPaymentId = paymentId;
    }

    public void Reopen()
    {
        if (Status != ChargeStatus.Paid)
            throw new DomainRuleException("payment.not_voidable");

        Status = ChargeStatus.Open;
        LatestPaymentId = null;
    }
}

public static class ReminderBilling
{
    public static int Mark(IEnumerable<Charge> charges)
    {
        var count = 0;
        foreach (var charge in charges)
            if (charge.Remind())
                count++;

        return count;
    }
}

public static class YearlyBilling
{
    public static IReadOnlyList<Charge> Raise(
        Guid tenantId,
        Guid cemeteryId,
        int year,
        IReadOnlyList<FeeSchedule> fees,
        IReadOnlyList<GraveSite> sites,
        IReadOnlySet<ChargeKey> already)
    {
        var billed = BillingYear.Require(year);
        var created = new List<Charge>();
        foreach (var site in sites)
            AddSite(created, tenantId, cemeteryId, billed, fees, site, already);

        return created;
    }

    private static void AddSite(
        List<Charge> created,
        Guid tenantId,
        Guid cemeteryId,
        int year,
        IReadOnlyList<FeeSchedule> fees,
        GraveSite site,
        IReadOnlySet<ChargeKey> already)
    {
        if (site.CemeteryId != cemeteryId || site.Closed)
            return;

        foreach (var fee in fees)
            AddFee(created, tenantId, year, site, fee, already);
    }

    private static void AddFee(
        List<Charge> created,
        Guid tenantId,
        int year,
        GraveSite site,
        FeeSchedule fee,
        IReadOnlySet<ChargeKey> already)
    {
        if (fee.CemeteryId != site.CemeteryId || fee.Kind != FeeKind.Annual)
            return;
        if (already.Contains(new ChargeKey(site.Id, fee.Id)))
            return;

        created.Add(Charge.Open(tenantId, site, fee, year));
    }
}
