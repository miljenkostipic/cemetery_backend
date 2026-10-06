using Cemetery.Domain.Identity;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Layout;

public sealed class Cemetery : TenantEntity
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 200;
    public const int PlanUrlMaxLength = 2000;

    private Cemetery()
    {
        Name = "";
    }

    public string Name { get; private set; }

    public bool Schematic { get; private set; }

    public string? PlanImageUrl { get; private set; }

    public GeoPolygon? PlanBounds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public int RestPeriodYears { get; private set; }

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return trimmed.Length >= NameMinLength && trimmed.Length <= NameMaxLength;
    }

    public static Cemetery Open(Guid tenantId, string name, bool schematic, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (!IsValidName(name))
            throw new DomainRuleException("cemetery.name_invalid");

        return new Cemetery
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Schematic = schematic,
            CreatedAt = createdAt,
            RestPeriodYears = RestPeriod.DefaultYears,
        };
    }

    public void SetRestPeriod(int years) => RestPeriodYears = RestPeriod.Require(years);

    public void SetPlan(string url, GeoPolygon bounds)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not "https" and not "http")
            throw new DomainRuleException("cemetery.plan_invalid");
        if (url.Length > PlanUrlMaxLength)
            throw new DomainRuleException("cemetery.plan_invalid");

        PlanImageUrl = url;
        PlanBounds = bounds;
    }
}
