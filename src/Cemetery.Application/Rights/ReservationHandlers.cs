using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record ReserveSite(Guid GraveSiteId, string GivenName, string FamilyName, string Email, DateOnly Until);

public sealed class ReserveSiteValidator : AbstractValidator<ReserveSite>
{
    public ReserveSiteValidator() => RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class ReserveSiteHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ReserveSite, ReservationView>
{
    public async Task<ReservationView> Handle(ReserveSite command, CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var holder = await Person(command, cancellationToken).ConfigureAwait(false);
        var reservation = Reservation.Hold(caller.OrganizationId, site.Id, holder.Id, command.Until, RegisterClock.Today(clock), site.Status);
        await rights.AddReservationAsync(reservation, cancellationToken).ConfigureAwait(false);
        await OccupancySync.Refresh(site, layouts, register, rights, clock, true, null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReservationView(reservation.Id, site.Id, holder.GivenName, holder.FamilyName, holder.Email ?? "", reservation.Until);
    }

    private async Task<Holder> Person(ReserveSite command, CancellationToken cancellationToken)
    {
        var existing = await rights.FindHolderByEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var created = Holder.Register(caller.OrganizationId, command.GivenName, command.FamilyName, null, null, command.Email, null);
        await rights.AddHolderAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }
}

public sealed record CancelReservation(Guid ReservationId);

public sealed class CancelReservationValidator : AbstractValidator<CancelReservation>
{
    public CancelReservationValidator() => RuleFor(command => command.ReservationId).NotEmpty().WithErrorCode("reservation.not_found");
}

public sealed class CancelReservationHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelReservation, SiteRightsView>
{
    public async Task<SiteRightsView> Handle(CancelReservation command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var reservation = await rights.FindReservationAsync(command.ReservationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("reservation.not_found");
        reservation.Cancel();
        var site = await layouts.FindGraveSiteAsync(reservation.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        await OccupancySync.Refresh(site, layouts, register, rights, clock, false, null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var right = await rights.FindActiveRightAsync(site.Id, cancellationToken).ConfigureAwait(false);
        return new SiteRightsView(right is null ? null : await RightViews.Of(rights, right, cancellationToken).ConfigureAwait(false), null);
    }
}
