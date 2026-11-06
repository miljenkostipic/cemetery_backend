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

public readonly record struct ChargeKey(Guid GraveSiteId, Guid FeeScheduleId, Guid? HolderId = null);

public readonly record struct BilledHolder(Guid HolderId, string Name, int Percent);

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

    public Guid? HolderId { get; private set; }

    public FeeKind Kind { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public ChargeStatus Status { get; private set; }

    public DateOnly DueOn { get; private set; }

    public int Year { get; private set; }

    public Guid? LatestPaymentId { get; private set; }

    public static Charge Open(Guid tenantId, GraveSite site, FeeSchedule fee, int year) =>
        Bill(tenantId, site, fee, year, null, "", fee.Amount);

    public static Charge Bill(Guid tenantId, GraveSite site, FeeSchedule fee, int year, Guid? holderId, string holderName, decimal amount)
    {
        RequirePlace(tenantId, site, fee);
        if (holderId == Guid.Empty || holderName.Length > HolderMaxLength || amount <= 0)
            throw new DomainRuleException(holderId == Guid.Empty ? "holder.not_found" : "charge.amount_invalid");

        return Create(tenantId, site, fee, BillingYear.Require(year), holderId, holderName, amount);
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

    private static Charge Create(Guid tenantId, GraveSite site, FeeSchedule fee, int year, Guid? holderId, string holderName, decimal amount) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = site.CemeteryId,
            GraveSiteId = site.Id,
            FeeScheduleId = fee.Id,
            GraveSiteCode = site.Code,
            HolderId = holderId,
            HolderName = holderName,
            Kind = fee.Kind,
            Description = fee.Name,
            Amount = amount,
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
        IReadOnlySet<ChargeKey> already) =>
        Raise(tenantId, cemeteryId, year, fees, sites, already, null);

    public static IReadOnlyList<Charge> Raise(
        Guid tenantId,
        Guid cemeteryId,
        int year,
        IReadOnlyList<FeeSchedule> fees,
        IReadOnlyList<GraveSite> sites,
        IReadOnlySet<ChargeKey> already,
        IReadOnlyDictionary<Guid, IReadOnlyList<BilledHolder>>? holders)
    {
        var billed = BillingYear.Require(year);
        var created = new List<Charge>();
        foreach (var site in sites)
            AddSite(created, tenantId, cemeteryId, billed, fees, site, already, holders);

        return created;
    }

    private static void AddSite(
        List<Charge> created,
        Guid tenantId,
        Guid cemeteryId,
        int year,
        IReadOnlyList<FeeSchedule> fees,
        GraveSite site,
        IReadOnlySet<ChargeKey> already,
        IReadOnlyDictionary<Guid, IReadOnlyList<BilledHolder>>? holders)
    {
        if (site.CemeteryId != cemeteryId || site.Closed)
            return;

        foreach (var fee in fees)
            AddFee(created, tenantId, year, site, fee, already, HoldersOf(holders, site.Id));
    }

    private static IReadOnlyList<BilledHolder> HoldersOf(IReadOnlyDictionary<Guid, IReadOnlyList<BilledHolder>>? holders, Guid siteId)
    {
        if (holders is not null && holders.TryGetValue(siteId, out var people))
            return people;

        return [];
    }

    private static void AddFee(
        List<Charge> created,
        Guid tenantId,
        int year,
        GraveSite site,
        FeeSchedule fee,
        IReadOnlySet<ChargeKey> already,
        IReadOnlyList<BilledHolder> holders)
    {
        if (fee.CemeteryId != site.CemeteryId || fee.Kind != FeeKind.Annual)
            return;
        if (holders.Count == 0)
            AddOpen(created, tenantId, year, site, fee, already);
        else
            AddHolders(created, tenantId, year, site, fee, already, holders);
    }

    private static void AddOpen(List<Charge> created, Guid tenantId, int year, GraveSite site, FeeSchedule fee, IReadOnlySet<ChargeKey> already)
    {
        if (already.Contains(new ChargeKey(site.Id, fee.Id)))
            return;

        created.Add(Charge.Open(tenantId, site, fee, year));
    }

    private static void AddHolders(
        List<Charge> created,
        Guid tenantId,
        int year,
        GraveSite site,
        FeeSchedule fee,
        IReadOnlySet<ChargeKey> already,
        IReadOnlyList<BilledHolder> holders)
    {
        decimal assigned = 0;
        for (var index = 0; index < holders.Count; index++)
            assigned = AddHolder(created, tenantId, year, site, fee, already, holders, index, assigned);
    }

    private static decimal AddHolder(
        List<Charge> created,
        Guid tenantId,
        int year,
        GraveSite site,
        FeeSchedule fee,
        IReadOnlySet<ChargeKey> already,
        IReadOnlyList<BilledHolder> holders,
        int index,
        decimal assigned)
    {
        var holder = holders[index];
        if (already.Contains(new ChargeKey(site.Id, fee.Id, holder.HolderId)))
            return assigned;

        var amount = index == holders.Count - 1 ? fee.Amount - assigned : decimal.Round(fee.Amount * holder.Percent / 100m, 2, MidpointRounding.AwayFromZero);
        created.Add(Charge.Bill(tenantId, site, fee, year, holder.HolderId, holder.Name, amount));
        return assigned + amount;
    }
}
