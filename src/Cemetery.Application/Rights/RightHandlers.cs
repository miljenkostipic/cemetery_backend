using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record GetSiteRights(Guid GraveSiteId);

public sealed class GetSiteRightsHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRightsRepository rights) : IQueryHandler<GetSiteRights, SiteRightsView>
{
    public async Task<SiteRightsView> Handle(GetSiteRights query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var right = await rights.FindActiveRightAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false);
        var reservation = await rights.FindActiveReservationAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false);
        return new SiteRightsView(await ViewAsync(right, cancellationToken).ConfigureAwait(false), await ReservationAsync(reservation, cancellationToken).ConfigureAwait(false));
    }

    private async Task<GraveRightView?> ViewAsync(GraveRight? right, CancellationToken cancellationToken) =>
        right is null ? null : await RightViews.Of(rights, right, cancellationToken).ConfigureAwait(false);

    private async Task<ReservationView?> ReservationAsync(Reservation? reservation, CancellationToken cancellationToken)
    {
        if (reservation is null)
            return null;
        var holder = await rights.FindHolderAsync(reservation.HolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        return new ReservationView(reservation.Id, reservation.GraveSiteId, holder.GivenName, holder.FamilyName, holder.Email ?? "", reservation.Until);
    }
}

public sealed record GrantRight(Guid GraveSiteId, bool Perpetual, int? TermYears, IReadOnlyList<HolderBody> Holders);

public sealed class GrantRightValidator : AbstractValidator<GrantRight>
{
    public GrantRightValidator() => RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class GrantRightHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<GrantRight, GraveRightView>
{
    public async Task<GraveRightView> Handle(GrantRight command, CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var current = await rights.FindActiveRightAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var shares = await RightViews.Shares(rights, caller.OrganizationId, command.Holders, cancellationToken).ConfigureAwait(false);
        var granted = GraveRight.Grant(caller.OrganizationId, site.Id, RegisterClock.Today(clock), command.TermYears, command.Perpetual, shares, current);
        await rights.AddRightAsync(granted, cancellationToken).ConfigureAwait(false);
        await OccupancySync.Refresh(site, layouts, register, rights, clock, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await RightViews.Of(rights, granted, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record RenewRight(Guid RightId, int Years);

public sealed class RenewRightValidator : AbstractValidator<RenewRight>
{
    public RenewRightValidator() => RuleFor(command => command.RightId).NotEmpty().WithErrorCode("right.not_found");
}

public sealed class RenewRightHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RenewRight, GraveRightView>
{
    public async Task<GraveRightView> Handle(RenewRight command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var right = await rights.FindRightAsync(command.RightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("right.not_found");
        right.Renew(command.Years);
        await RefreshAsync(right.GraveSiteId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await RightViews.Of(rights, right, cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshAsync(Guid graveSiteId, CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
        if (site is not null)
            await OccupancySync.Refresh(site, layouts, register, rights, clock, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record TransferRight(Guid RightId, DateOnly On, IReadOnlyList<HolderBody> Holders);

public sealed class TransferRightValidator : AbstractValidator<TransferRight>
{
    public TransferRightValidator() => RuleFor(command => command.RightId).NotEmpty().WithErrorCode("right.not_found");
}

public sealed class TransferRightHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<TransferRight, GraveRightView>
{
    public Task<GraveRightView> Handle(TransferRight command, CancellationToken cancellationToken) =>
        Convey.Run(caller, layouts, register, rights, unitOfWork, clock, command.RightId, command.On, command.Holders, static (right, on, shares) => right.Convey(on, shares), cancellationToken);
}

public sealed record InheritRight(Guid RightId, DateOnly On, IReadOnlyList<HolderBody> Holders);

public sealed class InheritRightValidator : AbstractValidator<InheritRight>
{
    public InheritRightValidator() => RuleFor(command => command.RightId).NotEmpty().WithErrorCode("right.not_found");
}

public sealed class InheritRightHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<InheritRight, GraveRightView>
{
    public Task<GraveRightView> Handle(InheritRight command, CancellationToken cancellationToken) =>
        Convey.Run(caller, layouts, register, rights, unitOfWork, clock, command.RightId, command.On, command.Holders, static (right, on, shares) => right.Inherit(on, shares), cancellationToken);
}

internal static class Convey
{
    public static async Task<GraveRightView> Run(
        ICallerContext caller,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IRightsRepository rights,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        Guid rightId,
        DateOnly on,
        IReadOnlyList<HolderBody> holders,
        Func<GraveRight, DateOnly, IReadOnlyList<RightShare>, GraveRight> next,
        CancellationToken cancellationToken)
    {
        var current = await rights.FindRightAsync(rightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("right.not_found");
        var created = next(current, on, await RightViews.Shares(rights, caller.OrganizationId, holders, cancellationToken).ConfigureAwait(false));
        await rights.AddRightAsync(created, cancellationToken).ConfigureAwait(false);
        await Refresh(layouts, register, rights, clock, current.GraveSiteId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await RightViews.Of(rights, created, cancellationToken).ConfigureAwait(false);
    }

    private static async Task Refresh(ILayoutRepository layouts, IRegisterRepository register, IRightsRepository rights, TimeProvider clock, Guid graveSiteId, CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
        if (site is not null)
            await OccupancySync.Refresh(site, layouts, register, rights, clock, cancellationToken).ConfigureAwait(false);
    }
}

internal static class RightViews
{
    public static async Task<IReadOnlyList<RightShare>> Shares(IRightsRepository rights, Guid tenantId, IReadOnlyList<HolderBody> holders, CancellationToken cancellationToken)
    {
        var shares = new List<RightShare>();
        foreach (var holder in holders)
            shares.Add(RightShare.Of(await Id(rights, tenantId, holder, cancellationToken).ConfigureAwait(false), holder.Share));
        return shares;
    }

    public static async Task<GraveRightView> Of(IRightsRepository rights, GraveRight right, CancellationToken cancellationToken)
    {
        var holders = new List<HolderView>();
        foreach (var share in right.Shares)
            holders.Add(await View(rights, share, cancellationToken).ConfigureAwait(false));
        return new GraveRightView(right.Id, right.GraveSiteId, RightsMaps.Status(right.Status), right.Perpetual, right.ValidFrom, right.ValidTo, holders);
    }

    private static async Task<Guid> Id(IRightsRepository rights, Guid tenantId, HolderBody body, CancellationToken cancellationToken)
    {
        var existing = await rights.FindHolderByEmailAsync(body.Email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return existing.Id;

        var created = Holder.Register(tenantId, body.GivenName, body.FamilyName, null, null, body.Email, null);
        await rights.AddHolderAsync(created, cancellationToken).ConfigureAwait(false);
        return created.Id;
    }

    private static async Task<HolderView> View(IRightsRepository rights, RightShare share, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderAsync(share.HolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        return new HolderView(holder.Id, holder.GivenName, holder.FamilyName, holder.Email ?? "", share.Percent);
    }
}
