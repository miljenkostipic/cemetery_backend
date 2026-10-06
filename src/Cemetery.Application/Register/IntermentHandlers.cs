using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Register;

public sealed record ListFreePositions(Guid CemeteryId, Guid? GraveSiteId);

public sealed class ListFreePositionsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    TimeProvider clock) : IQueryHandler<ListFreePositions, IReadOnlyList<FreePositionView>>
{
    public async Task<IReadOnlyList<FreePositionView>> Handle(ListFreePositions query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var interments = await register.ListIntermentsByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return RegisterMaps.Free(sites, interments, query.GraveSiteId, RestYears.Of(cemetery), RegisterClock.Today(clock));
    }
}

public sealed record RecordInterment(Guid DeceasedId, Guid GraveSiteId, int Position, string Kind, DateOnly BuriedOn);

public sealed class RecordIntermentValidator : AbstractValidator<RecordInterment>
{
    public RecordIntermentValidator()
    {
        RuleFor(command => command.DeceasedId).NotEmpty().WithErrorCode("deceased.not_found");
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
        RuleFor(command => command.Position).GreaterThan(0).WithErrorCode("interment.position_invalid");
        RuleFor(command => command.Kind).Must(code => IntermentKinds.TryParse(code, out _)).WithErrorCode("interment.kind_invalid");
    }
}

public sealed class RecordIntermentHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RecordInterment, IntermentView>
{
    public async Task<IntermentView> Handle(RecordInterment command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var created = await PlaceAsync(command, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task<IntermentView> PlaceAsync(RecordInterment command, CancellationToken cancellationToken)
    {
        var deceased = await register.FindDeceasedAsync(command.DeceasedId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("deceased.not_found");
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var cemetery = await layouts.FindCemeteryAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var atSite = await register.ListIntermentsBySiteAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var ofPerson = await register.ListIntermentsByDeceasedAsync(deceased.Id, cancellationToken).ConfigureAwait(false);
        var created = BurialBook.Place(deceased, site, command.Position, IntermentKinds.Parse(command.Kind), command.BuriedOn, atSite, ofPerson, RestYears.Of(cemetery));
        await register.AddIntermentAsync(created, cancellationToken).ConfigureAwait(false);
        await register.AddAuditAsync(AuditEntry.Write(created.TenantId, AuditActions.IntermentRecorded, created.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(created, deceased, site.Code);
    }
}

public sealed record ExhumeInterment(Guid IntermentId, DateOnly On);

public sealed class ExhumeIntermentValidator : AbstractValidator<ExhumeInterment>
{
    public ExhumeIntermentValidator() =>
        RuleFor(command => command.IntermentId).NotEmpty().WithErrorCode("interment.not_found");
}

public sealed class ExhumeIntermentHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ExhumeInterment, IntermentView>
{
    public async Task<IntermentView> Handle(ExhumeInterment command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var interment = await register.FindIntermentAsync(command.IntermentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("interment.not_found");
        var deceased = await register.FindDeceasedAsync(interment.DeceasedId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("deceased.not_found");
        var site = await layouts.FindGraveSiteAsync(interment.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        interment.Exhume(command.On);
        await register.AddAuditAsync(AuditEntry.Write(interment.TenantId, AuditActions.IntermentExhumed, interment.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(interment, deceased, site.Code);
    }
}

public sealed record TransferInterment(Guid IntermentId, Guid GraveSiteId, int Position, DateOnly On);

public sealed class TransferIntermentValidator : AbstractValidator<TransferInterment>
{
    public TransferIntermentValidator()
    {
        RuleFor(command => command.IntermentId).NotEmpty().WithErrorCode("interment.not_found");
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
        RuleFor(command => command.Position).GreaterThan(0).WithErrorCode("interment.position_invalid");
    }
}

public sealed class TransferIntermentHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<TransferInterment, IntermentView>
{
    public async Task<IntermentView> Handle(TransferInterment command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var moved = await MoveAsync(command, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return moved;
    }

    private async Task<IntermentView> MoveAsync(TransferInterment command, CancellationToken cancellationToken)
    {
        var source = await register.FindIntermentAsync(command.IntermentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("interment.not_found");
        var destination = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var cemetery = await layouts.FindCemeteryAsync(destination.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var deceased = await register.FindDeceasedAsync(source.DeceasedId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("deceased.not_found");
        var atDestination = await register.ListIntermentsBySiteAsync(destination.Id, cancellationToken).ConfigureAwait(false);
        var created = BurialBook.Transfer(source, destination, command.Position, command.On, atDestination, RestYears.Of(cemetery));
        await register.AddIntermentAsync(created, cancellationToken).ConfigureAwait(false);
        await register.AddAuditAsync(AuditEntry.Write(created.TenantId, AuditActions.IntermentTransferred, created.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(created, deceased, destination.Code);
    }
}

public sealed record ListSiteInterments(Guid GraveSiteId);

public sealed class ListSiteIntermentsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register) : IQueryHandler<ListSiteInterments, IReadOnlyList<IntermentView>>
{
    public async Task<IReadOnlyList<IntermentView>> Handle(ListSiteInterments query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var interments = await register.ListIntermentsBySiteAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var views = new List<IntermentView>();
        foreach (var interment in interments)
        {
            if (!interment.Occupies)
                continue;
            var person = await register.FindDeceasedAsync(interment.DeceasedId, cancellationToken).ConfigureAwait(false);
            if (person is not null)
                views.Add(RegisterMaps.ToView(interment, person, site.Code));
        }

        return views;
    }
}
