using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using FluentValidation;

namespace Cemetery.Application.Operations;

public sealed record OpenWorkOrder(Guid CemeteryId, Guid GraveSiteId, string Kind, DateOnly ScheduledOn, string Notes);

public sealed class OpenWorkOrderValidator : AbstractValidator<OpenWorkOrder>
{
    public OpenWorkOrderValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("work_order.site_invalid");
        RuleFor(command => command.Kind).NotEmpty().WithErrorCode("work_order.kind_invalid");
        RuleFor(command => command.ScheduledOn).Must(day => day != default).WithErrorCode("work_order.date_invalid");
    }
}

public sealed class OpenWorkOrderHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<OpenWorkOrder, WorkOrderView>
{
    public async Task<WorkOrderView> Handle(OpenWorkOrder command, CancellationToken cancellationToken)
    {
        var organizationId = await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await OperationsGate.RequireCemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var site = await OperationsGate.RequireSiteAsync(layouts, command.CemeteryId, command.GraveSiteId, "work_order.site_invalid", cancellationToken).ConfigureAwait(false);
        var order = await Save(organizationId, command, cancellationToken).ConfigureAwait(false);
        return OperationsMaps.Order(order, site);
    }

    private async Task<WorkOrder> Save(Guid organizationId, OpenWorkOrder command, CancellationToken cancellationToken)
    {
        var order = WorkOrder.Open(organizationId, command.CemeteryId, command.GraveSiteId, command.Kind, command.ScheduledOn, command.Notes);
        await AttachPermit(order, cancellationToken).ConfigureAwait(false);
        await store.AddWorkOrderAsync(order, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order;
    }

    private async Task AttachPermit(WorkOrder order, CancellationToken cancellationToken)
    {
        if (order.Kind != WorkKind.Monument)
            return;

        var permits = await store.ListPermitsAsync(order.CemeteryId, cancellationToken).ConfigureAwait(false);
        var cover = MonumentPermit.BestCover(permits, order.GraveSiteId, order.ScheduledOn);
        if (cover is not null)
            order.UsePermit(cover.Id);
    }
}

public sealed record ListWorkOrders(Guid CemeteryId);

public sealed class ListWorkOrdersHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store) : IQueryHandler<ListWorkOrders, IReadOnlyList<WorkOrderView>>
{
    public async Task<IReadOnlyList<WorkOrderView>> Handle(ListWorkOrders query, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var orders = await store.ListWorkOrdersAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = OperationsIndex.ById(await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false), site => site.Id);
        return orders.Select(order => Map(order, sites)).ToArray();
    }

    private static WorkOrderView Map(WorkOrder order, Dictionary<Guid, GraveSite> sites)
    {
        sites.TryGetValue(order.GraveSiteId, out var site);
        return OperationsMaps.Order(order, site);
    }
}

public sealed record CompleteWorkOrder(Guid WorkOrderId, DateOnly CompletedOn, string Note);

public sealed class CompleteWorkOrderValidator : AbstractValidator<CompleteWorkOrder>
{
    public CompleteWorkOrderValidator()
    {
        RuleFor(command => command.WorkOrderId).NotEmpty().WithErrorCode("work_order.not_found");
        RuleFor(command => command.CompletedOn).Must(day => day != default).WithErrorCode("completion.date_invalid");
    }
}

public sealed class CompleteWorkOrderHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<CompleteWorkOrder, CompletionView>
{
    public async Task<CompletionView> Handle(CompleteWorkOrder command, CancellationToken cancellationToken)
    {
        _ = await OperationsAccess.RequireFieldAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var order = await store.FindWorkOrderAsync(command.WorkOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("work_order.not_found");
        order.Complete(command.CompletedOn, command.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CompletionView(order.Id, WorkOrderStatuses.ToCode(order.Status), command.CompletedOn);
    }
}

public sealed record AttachWorkOrderPhoto(Guid WorkOrderId, string ContentType, string ContentBase64);

public sealed class AttachWorkOrderPhotoValidator : AbstractValidator<AttachWorkOrderPhoto>
{
    public AttachWorkOrderPhotoValidator() =>
        RuleFor(command => command.WorkOrderId).NotEmpty().WithErrorCode("work_order.not_found");
}

public sealed class AttachWorkOrderPhotoHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<AttachWorkOrderPhoto, PhotoView>
{
    public async Task<PhotoView> Handle(AttachWorkOrderPhoto command, CancellationToken cancellationToken)
    {
        var organizationId = await OperationsAccess.RequireFieldAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var order = await store.FindWorkOrderAsync(command.WorkOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("work_order.not_found");
        var photo = WorkOrderPhoto.Record(organizationId, order.Id, command.ContentType, command.ContentBase64);
        await store.AddPhotoAsync(photo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PhotoView(photo.Id, photo.WorkOrderId, photo.ContentType);
    }
}
