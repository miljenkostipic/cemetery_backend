using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public static class AuditActions
{
    public const string DeceasedRecorded = "deceased.recorded";
    public const string DeceasedCorrected = "deceased.corrected";
    public const string DeceasedRemoved = "deceased.removed";
    public const string IntermentRecorded = "interment.recorded";
    public const string IntermentExhumed = "interment.exhumed";
    public const string IntermentTransferred = "interment.transferred";
    public const string RestPeriodSet = "cemetery.rest_period_set";

    public static bool IsKnown(string? action) => action is
        DeceasedRecorded or DeceasedCorrected or DeceasedRemoved or IntermentRecorded or IntermentExhumed or IntermentTransferred or RestPeriodSet;
}

public sealed class AuditEntry : TenantEntity
{
    private AuditEntry()
    {
        Action = "";
        Reason = "";
    }

    public string Action { get; private set; }

    public Guid SubjectId { get; private set; }

    public string Reason { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static AuditEntry Write(Guid tenantId, string action, Guid subjectId, string? reason, DateTimeOffset at)
    {
        if (tenantId == Guid.Empty || subjectId == Guid.Empty || !AuditActions.IsKnown(action))
            throw new DomainRuleException("audit.invalid");

        var text = string.IsNullOrWhiteSpace(reason) ? action : reason.Trim();
        if (text.Length > CorrectionReason.MaxLength)
            throw new DomainRuleException("audit.invalid");

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Action = action,
            SubjectId = subjectId,
            Reason = text,
            At = at,
        };
    }
}
