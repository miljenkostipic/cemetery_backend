using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Launch;

public sealed record GdprRecord(Guid Id, string Purpose, string Categories, string Retention, string LawfulBasis);

public static class GdprCatalog
{
    public static readonly Guid HoldersId = Guid.Parse("6f0c1a10-0000-7000-8000-000000000001");

    public static readonly Guid PaymentsId = Guid.Parse("6f0c1a10-0000-7000-8000-000000000002");

    public static readonly Guid DocumentsId = Guid.Parse("6f0c1a10-0000-7000-8000-000000000003");

    public static readonly Guid AuditId = Guid.Parse("6f0c1a10-0000-7000-8000-000000000004");

    public static IReadOnlyList<GdprRecord> Records { get; } =
    [
        new(HoldersId, "holders", "contact", "right_term", "contract"),
        new(PaymentsId, "payments", "payments", "statutory", "legal_obligation"),
        new(DocumentsId, "documents", "documents", "right_term", "contract"),
        new(AuditId, "audit", "identity", "account", "legitimate_interest"),
    ];
}

public sealed class GdprExport : TenantEntity
{
    public const string Requested = "requested";

    private GdprExport()
    {
        Status = "";
    }

    public DateTimeOffset RequestedAt { get; private set; }

    public string Status { get; private set; }

    public static GdprExport Open(Guid tenantId, DateTimeOffset requestedAt)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");

        return new GdprExport
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            RequestedAt = requestedAt,
            Status = Requested,
        };
    }
}
