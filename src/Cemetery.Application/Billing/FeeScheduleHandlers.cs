using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Billing;
using FluentValidation;

namespace Cemetery.Application.Billing;

public sealed record ListFeeSchedules(Guid CemeteryId);

public sealed class ListFeeSchedulesHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IBillingRepository billing) : IQueryHandler<ListFeeSchedules, IReadOnlyList<FeeScheduleView>>
{
    public async Task<IReadOnlyList<FeeScheduleView>> Handle(ListFeeSchedules query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await BillingAccess.RequireCemeteryAsync(layouts, query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var fees = await billing.ListFeesAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return fees.Select(BillingMaps.Fee).ToArray();
    }
}

public sealed record AddFeeSchedule(Guid CemeteryId, string Kind, string Name, string Amount, string Currency);

public sealed class AddFeeScheduleValidator : AbstractValidator<AddFeeSchedule>
{
    public AddFeeScheduleValidator()
    {
        RuleFor(command => command.Kind).Must(FeeKinds.IsKnown).WithErrorCode("fee.kind_invalid");
        RuleFor(command => command.Name).Must(FeeName.Accepts).WithErrorCode(FeeName.Invalid);
        RuleFor(command => command.Amount).Must(FeeAmount.Accepts).WithErrorCode(FeeAmount.Invalid);
        RuleFor(command => command.Currency).Must(FeeCurrencies.IsKnown).WithErrorCode("fee.currency_invalid");
    }
}

public sealed class AddFeeScheduleHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<AddFeeSchedule, FeeScheduleView>
{
    public async Task<FeeScheduleView> Handle(AddFeeSchedule command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await BillingAccess.RequireCemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var fee = FeeSchedule.Add(organizationId, command.CemeteryId, command.Kind, command.Name, command.Amount, command.Currency);
        await billing.AddFeeAsync(fee, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Fee(fee);
    }
}

public sealed record RemoveFeeSchedule(Guid FeeScheduleId);

public sealed class RemoveFeeScheduleValidator : AbstractValidator<RemoveFeeSchedule>
{
    public RemoveFeeScheduleValidator() =>
        RuleFor(command => command.FeeScheduleId).NotEmpty().WithErrorCode("fee.not_found");
}

public sealed class RemoveFeeScheduleHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveFeeSchedule, bool>
{
    public async Task<bool> Handle(RemoveFeeSchedule command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var fee = await billing.FindFeeAsync(command.FeeScheduleId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("fee.not_found");
        await RejectWhenCharged(fee.Id, cancellationToken).ConfigureAwait(false);
        billing.RemoveFee(fee);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task RejectWhenCharged(Guid feeId, CancellationToken cancellationToken)
    {
        if (await billing.FeeIsChargedAsync(feeId, cancellationToken).ConfigureAwait(false))
            throw new ConflictException("fee.in_use");
    }
}
