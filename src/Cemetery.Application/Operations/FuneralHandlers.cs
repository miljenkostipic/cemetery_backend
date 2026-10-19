using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Operations;

public sealed record ScheduleFuneral(Guid CemeteryId, Guid DeceasedId, Guid GraveSiteId, string StartsAt, string EndsAt, string Place);

public sealed class ScheduleFuneralValidator : AbstractValidator<ScheduleFuneral>
{
    public ScheduleFuneralValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.DeceasedId).NotEmpty().WithErrorCode("funeral.deceased_invalid");
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("funeral.site_invalid");
        RuleFor(command => command.StartsAt).NotEmpty().WithErrorCode("funeral.date_invalid");
        RuleFor(command => command.EndsAt).NotEmpty().WithErrorCode("funeral.date_invalid");
        RuleFor(command => command.Place).NotEmpty().WithErrorCode("funeral.place_invalid");
    }
}

public sealed class ScheduleFuneralHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IOperationsRepository store,
    IWebhookRepository webhooks,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ScheduleFuneral, FuneralView>
{
    public async Task<FuneralView> Handle(ScheduleFuneral command, CancellationToken cancellationToken)
    {
        var organizationId = await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await OperationsGate.RequireCemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var person = await OperationsGate.RequirePersonAsync(register, command.CemeteryId, command.DeceasedId, cancellationToken).ConfigureAwait(false);
        var site = await OperationsGate.RequireSiteAsync(layouts, command.CemeteryId, command.GraveSiteId, "funeral.site_invalid", cancellationToken).ConfigureAwait(false);
        var funeral = await Save(organizationId, command, cancellationToken).ConfigureAwait(false);
        return OperationsMaps.Funeral(funeral, person, site);
    }

    private async Task<Funeral> Save(Guid organizationId, ScheduleFuneral command, CancellationToken cancellationToken)
    {
        var funeral = Funeral.Schedule(organizationId, command.CemeteryId, command.DeceasedId, command.GraveSiteId, command.StartsAt, command.EndsAt, command.Place);
        await RejectClash(command.CemeteryId, funeral, cancellationToken).ConfigureAwait(false);
        await store.AddFuneralAsync(funeral, cancellationToken).ConfigureAwait(false);
        await webhooks.EnqueueAsync(WebhookEvents.FuneralScheduled, funeral.Id, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return funeral;
    }

    private async Task RejectClash(Guid cemeteryId, Funeral funeral, CancellationToken cancellationToken)
    {
        var funerals = await store.ListFuneralsAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        var bookings = await store.ListBookingsAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        if (funerals.Any(item => funeral.Clashes(item)) || bookings.Any(item => funeral.Clashes(item)))
            throw new ConflictException("funeral.conflict");
    }
}

public sealed record CancelFuneral(Guid FuneralId);

public sealed class CancelFuneralValidator : AbstractValidator<CancelFuneral>
{
    public CancelFuneralValidator() =>
        RuleFor(command => command.FuneralId).NotEmpty().WithErrorCode("funeral.not_found");
}

public sealed class CancelFuneralHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelFuneral, FuneralView>
{
    public async Task<FuneralView> Handle(CancelFuneral command, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var funeral = await store.FindFuneralAsync(command.FuneralId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("funeral.not_found");
        funeral.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await View(funeral, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FuneralView> View(Funeral funeral, CancellationToken cancellationToken)
    {
        var person = await register.FindDeceasedAsync(funeral.DeceasedId, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(funeral.GraveSiteId, cancellationToken).ConfigureAwait(false);
        return OperationsMaps.Funeral(funeral, person, site);
    }
}

public sealed record ListFunerals(Guid CemeteryId);

public sealed class ListFuneralsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IOperationsRepository store) : IQueryHandler<ListFunerals, IReadOnlyList<FuneralView>>
{
    public async Task<IReadOnlyList<FuneralView>> Handle(ListFunerals query, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var funerals = await store.ListFuneralsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var people = OperationsIndex.ById(await register.ListDeceasedAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false), person => person.Id);
        var sites = OperationsIndex.ById(await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false), site => site.Id);
        return funerals.Select(funeral => Map(funeral, people, sites)).ToArray();
    }

    private static FuneralView Map(Funeral funeral, Dictionary<Guid, Deceased> people, Dictionary<Guid, GraveSite> sites)
    {
        people.TryGetValue(funeral.DeceasedId, out var person);
        sites.TryGetValue(funeral.GraveSiteId, out var site);
        return OperationsMaps.Funeral(funeral, person, site);
    }
}
