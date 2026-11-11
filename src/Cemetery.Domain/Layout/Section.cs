using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public sealed class Section : TenantEntity, IStampable
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 80;
    public const int CodeMaxLength = 8;

    private Section()
    {
        Name = "";
        Code = "";
    }

    public Guid CemeteryId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public GeoPolygon? Outline { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var trimmed = name.Trim();
        return trimmed.Length >= NameMinLength && trimmed.Length <= NameMaxLength;
    }

    public static bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        var trimmed = code.Trim();
        return trimmed.Length <= CodeMaxLength && trimmed.All(char.IsAsciiLetterOrDigit);
    }

    public static Section Add(Guid tenantId, Guid cemeteryId, string name, string code, GeoPolygon? outline)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("section.cemetery_required");
        if (!IsValidName(name))
            throw new DomainRuleException("section.name_invalid");
        if (!IsValidCode(code))
            throw new DomainRuleException("section.code_invalid");

        return new Section
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Outline = outline,
        };
    }

    public void ReplaceOutline(GeoPolygon outline) => Outline = outline;

    /// <summary>A grave placed in the section must reach into its outline, when it has one.</summary>
    public void RejectOutside(GeoPolygon graveOutline)
    {
        if (Outline is not null && !Outline.InteriorOverlaps(graveOutline))
            throw new DomainRuleException("grave_site.outside_section");
    }

    public void Stamp(DateTimeOffset at) => UpdatedAt = at;
}
