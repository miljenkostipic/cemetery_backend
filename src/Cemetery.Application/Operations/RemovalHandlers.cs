using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using FluentValidation;

namespace Cemetery.Application.Operations;

public sealed record RemoveFuneral(Guid FuneralId);

public sealed class RemoveFuneralValidator : AbstractValidator<RemoveFuneral>
{
    public RemoveFuneralValidator() =>
        RuleFor(command => command.FuneralId).NotEmpty().WithErrorCode("funeral.not_found");
}

public sealed class RemoveFuneralHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveFuneral, bool>
{
    public async Task<bool> Handle(RemoveFuneral command, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var funeral = await store.FindFuneralAsync(command.FuneralId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("funeral.not_found");
        store.RemoveFuneral(funeral);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed record RemoveChapelBooking(Guid BookingId);

public sealed class RemoveChapelBookingValidator : AbstractValidator<RemoveChapelBooking>
{
    public RemoveChapelBookingValidator() =>
        RuleFor(command => command.BookingId).NotEmpty().WithErrorCode("chapel.not_found");
}

public sealed class RemoveChapelBookingHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveChapelBooking, bool>
{
    public async Task<bool> Handle(RemoveChapelBooking command, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var booking = await store.FindBookingAsync(command.BookingId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("chapel.not_found");
        store.RemoveBooking(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed record RemoveWorkOrder(Guid WorkOrderId);

public sealed class RemoveWorkOrderValidator : AbstractValidator<RemoveWorkOrder>
{
    public RemoveWorkOrderValidator() =>
        RuleFor(command => command.WorkOrderId).NotEmpty().WithErrorCode("work_order.not_found");
}

public sealed class RemoveWorkOrderHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveWorkOrder, bool>
{
    public async Task<bool> Handle(RemoveWorkOrder command, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var order = await store.FindWorkOrderAsync(command.WorkOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("work_order.not_found");
        await store.RemoveWorkOrderAsync(order, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed record RemoveMonumentPermit(Guid PermitId);

public sealed class RemoveMonumentPermitValidator : AbstractValidator<RemoveMonumentPermit>
{
    public RemoveMonumentPermitValidator() =>
        RuleFor(command => command.PermitId).NotEmpty().WithErrorCode("permit.not_found");
}

public sealed class RemoveMonumentPermitHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveMonumentPermit, bool>
{
    public async Task<bool> Handle(RemoveMonumentPermit command, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var permit = await store.FindPermitAsync(command.PermitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("permit.not_found");
        await store.RemovePermitAsync(permit, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
