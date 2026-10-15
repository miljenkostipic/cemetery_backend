using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public enum WorkKind
{
    Digging = 1,
    Monument = 2,
    Maintenance = 3,
}

public enum WorkOrderStatus
{
    Open = 1,
    Completed = 2,
}

public static class WorkKinds
{
    public static string ToCode(WorkKind kind) => kind switch
    {
        WorkKind.Digging => "digging",
        WorkKind.Monument => "monument",
        WorkKind.Maintenance => "maintenance",
        _ => throw new DomainRuleException("work_order.kind_invalid"),
    };

    public static WorkKind Parse(string? code) => code switch
    {
        "digging" => WorkKind.Digging,
        "monument" => WorkKind.Monument,
        "maintenance" => WorkKind.Maintenance,
        _ => throw new DomainRuleException("work_order.kind_invalid"),
    };
}

public static class WorkOrderStatuses
{
    public static string ToCode(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => "open",
        WorkOrderStatus.Completed => "completed",
        _ => throw new DomainRuleException("work_order.kind_invalid"),
    };

    public static WorkOrderStatus Parse(string? code) => code switch
    {
        "open" => WorkOrderStatus.Open,
        "completed" => WorkOrderStatus.Completed,
        _ => throw new DomainRuleException("work_order.kind_invalid"),
    };
}

public sealed class WorkOrder : TenantEntity
{
    private WorkOrder()
    {
        Notes = "";
    }

    public Guid CemeteryId { get; private set; }

    public Guid GraveSiteId { get; private set; }

    public WorkKind Kind { get; private set; }

    public WorkOrderStatus Status { get; private set; }

    public DateOnly ScheduledOn { get; private set; }

    public string Notes { get; private set; }

    public Guid? PermitId { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public string? CompletionNote { get; private set; }

    public static WorkOrder Open(Guid tenantId, Guid cemeteryId, Guid graveSiteId, string? kind, DateOnly scheduledOn, string? notes)
    {
        RequireParties(tenantId, cemeteryId, graveSiteId);
        if (scheduledOn == default)
            throw new DomainRuleException("work_order.date_invalid");

        return new WorkOrder
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            GraveSiteId = graveSiteId,
            Kind = WorkKinds.Parse(kind),
            Status = WorkOrderStatus.Open,
            ScheduledOn = scheduledOn,
            Notes = OperationsText.Note(notes, "work_order.notes_invalid"),
        };
    }

    public void UsePermit(Guid permitId)
    {
        if (Kind != WorkKind.Monument)
            throw new DomainRuleException("work_order.kind_invalid");
        if (permitId == Guid.Empty)
            throw new DomainRuleException("permit.site_invalid");
        PermitId = permitId;
    }

    public void ClearPermit() => PermitId = null;

    public void Complete(DateOnly completedOn, string? note)
    {
        if (Status == WorkOrderStatus.Completed)
            throw new DomainRuleException("work_order.closed");
        if (completedOn == default)
            throw new DomainRuleException("completion.date_invalid");

        Status = WorkOrderStatus.Completed;
        CompletedOn = completedOn;
        CompletionNote = OperationsText.Note(note, "completion.note_invalid");
    }

    private static void RequireParties(Guid tenantId, Guid cemeteryId, Guid graveSiteId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("cemetery.not_found");
        if (graveSiteId == Guid.Empty)
            throw new DomainRuleException("work_order.site_invalid");
    }
}
