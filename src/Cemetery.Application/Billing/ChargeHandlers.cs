using Cemetery.Application;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Register;
using Cemetery.Domain.Billing;
using FluentValidation;

namespace Cemetery.Application.Billing;

public sealed record ListCharges(Guid CemeteryId);

public sealed class ListChargesHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IBillingRepository billing) : IQueryHandler<ListCharges, IReadOnlyList<ChargeView>>
{
    public async Task<IReadOnlyList<ChargeView>> Handle(ListCharges query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        await Require.CemeteryAsync(layouts, query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var charges = await billing.ListChargesAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return charges.Select(BillingMaps.Charge).ToArray();
    }
}

public sealed record ListArrears(Guid CemeteryId);

public sealed class ListArrearsHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IBillingRepository billing) : IQueryHandler<ListArrears, IReadOnlyList<ArrearsRow>>
{
    public async Task<IReadOnlyList<ArrearsRow>> Handle(ListArrears query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        await Require.CemeteryAsync(layouts, query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var charges = await billing.ListArrearsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return charges.Select(BillingMaps.Arrears).ToArray();
    }
}

public sealed record RunYear(Guid CemeteryId, int Year);

public sealed class RunYearValidator : AbstractValidator<RunYear>
{
    public RunYearValidator() =>
        RuleFor(command => command.Year).Must(BillingYear.Accepts).WithErrorCode(BillingYear.Invalid);
}

public sealed class RunYearHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<RunYear, YearlyRunView>
{
    public async Task<YearlyRunView> Handle(RunYear command, CancellationToken cancellationToken)
    {
        var organizationId = caller.OrganizationId;
        await Require.CemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var year = BillingYear.Require(command.Year);
        var created = await RaiseAsync(organizationId, command.CemeteryId, year, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new YearlyRunView(year, created);
    }

    private async Task<int> RaiseAsync(Guid organizationId, Guid cemeteryId, int year, CancellationToken cancellationToken)
    {
        var fees = await billing.ListFeesAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = await layouts.ListByCemeteryAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        var already = await billing.ChargedPairsAsync(cemeteryId, year, cancellationToken).ConfigureAwait(false);
        var charges = YearlyBilling.Raise(organizationId, cemeteryId, year, fees, sites, already);
        await billing.AddChargesAsync(charges, cancellationToken).ConfigureAwait(false);
        return charges.Count;
    }
}

public sealed record SendReminders(Guid CemeteryId);

public sealed class SendRemindersValidator : AbstractValidator<SendReminders>
{
    public SendRemindersValidator() =>
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
}

public sealed class SendRemindersHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<SendReminders, ReminderRunView>
{
    public async Task<ReminderRunView> Handle(SendReminders command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        await Require.CemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var count = await MarkAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReminderRunView(count);
    }

    private async Task<int> MarkAsync(Guid cemeteryId, CancellationToken cancellationToken)
    {
        var charges = await billing.ListOpenChargesAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        return ReminderBilling.Mark(charges);
    }
}
