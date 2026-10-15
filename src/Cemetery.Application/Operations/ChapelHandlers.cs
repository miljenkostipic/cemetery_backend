using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Operations;
using FluentValidation;

namespace Cemetery.Application.Operations;

public sealed record BookChapel(Guid CemeteryId, string Place, string StartsAt, string EndsAt);

public sealed class BookChapelValidator : AbstractValidator<BookChapel>
{
    public BookChapelValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.Place).NotEmpty().WithErrorCode("chapel.place_invalid");
        RuleFor(command => command.StartsAt).NotEmpty().WithErrorCode("chapel.date_invalid");
        RuleFor(command => command.EndsAt).NotEmpty().WithErrorCode("chapel.date_invalid");
    }
}

public sealed class BookChapelHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store,
    IUnitOfWork unitOfWork) : ICommandHandler<BookChapel, ChapelBookingView>
{
    public async Task<ChapelBookingView> Handle(BookChapel command, CancellationToken cancellationToken)
    {
        var organizationId = await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await OperationsGate.RequireCemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var booking = await Save(organizationId, command, cancellationToken).ConfigureAwait(false);
        return OperationsMaps.Booking(booking);
    }

    private async Task<ChapelBooking> Save(Guid organizationId, BookChapel command, CancellationToken cancellationToken)
    {
        var booking = ChapelBooking.Book(organizationId, command.CemeteryId, command.Place, command.StartsAt, command.EndsAt);
        await RejectClash(command.CemeteryId, booking, cancellationToken).ConfigureAwait(false);
        await store.AddBookingAsync(booking, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return booking;
    }

    private async Task RejectClash(Guid cemeteryId, ChapelBooking booking, CancellationToken cancellationToken)
    {
        var bookings = await store.ListBookingsAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        var funerals = await store.ListFuneralsAsync(cemeteryId, cancellationToken).ConfigureAwait(false);
        if (bookings.Any(item => booking.Clashes(item)) || funerals.Any(item => booking.Clashes(item)))
            throw new ConflictException("chapel.conflict");
    }
}

public sealed record ListChapelBookings(Guid CemeteryId);

public sealed class ListChapelBookingsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOperationsRepository store) : IQueryHandler<ListChapelBookings, IReadOnlyList<ChapelBookingView>>
{
    public async Task<IReadOnlyList<ChapelBookingView>> Handle(ListChapelBookings query, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var bookings = await store.ListBookingsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return bookings.Select(OperationsMaps.Booking).ToArray();
    }
}
