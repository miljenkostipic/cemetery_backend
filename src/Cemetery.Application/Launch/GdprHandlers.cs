using Cemetery.Application.Abstractions;
using Cemetery.Application.Register;
using Cemetery.Domain.Launch;
using FluentValidation;

namespace Cemetery.Application.Launch;

public sealed record GdprRecordView(Guid Id, string Purpose, string Categories, string Retention, string LawfulBasis);

public sealed record GdprExportView(Guid Id, DateTimeOffset RequestedAt, string Status);

public sealed record ListGdprRecords;

public sealed class ListGdprRecordsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships) : IQueryHandler<ListGdprRecords, IReadOnlyList<GdprRecordView>>
{
    public async Task<IReadOnlyList<GdprRecordView>> Handle(ListGdprRecords query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        return GdprCatalog.Records.Select(static record => new GdprRecordView(record.Id, record.Purpose, record.Categories, record.Retention, record.LawfulBasis)).ToArray();
    }
}

public sealed record RequestGdprExport;

public sealed class RequestGdprExportValidator : AbstractValidator<RequestGdprExport>
{
    public RequestGdprExportValidator() => RuleFor(command => command).NotNull().WithErrorCode("gdpr.not_found");
}

public sealed class RequestGdprExportHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IGdprExportRepository exports,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RequestGdprExport, GdprExportView>
{
    public async Task<GdprExportView> Handle(RequestGdprExport command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var created = GdprExport.Open(organizationId, clock.GetUtcNow());
        await exports.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new GdprExportView(created.Id, created.RequestedAt, created.Status);
    }
}
