using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public sealed class GraveRow : TenantEntity
{
    public const int LabelMaxLength = 16;

    private GraveRow()
    {
        Label = "";
    }

    public Guid SectionId { get; private set; }

    public string Label { get; private set; }

    public static bool IsValidLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;
        var trimmed = label.Trim();
        return trimmed.Length <= LabelMaxLength;
    }

    public static GraveRow Add(Guid tenantId, Guid sectionId, string label)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (sectionId == Guid.Empty)
            throw new DomainRuleException("row.section_required");
        if (!IsValidLabel(label))
            throw new DomainRuleException("row.label_invalid");

        return new GraveRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SectionId = sectionId,
            Label = label.Trim(),
        };
    }
}
