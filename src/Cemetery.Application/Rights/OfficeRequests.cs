using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record OfficeRequestView(Guid Id, string Message, DateOnly CreatedOn, Guid? GraveSiteId);

public sealed record ListOfficeRequests;

public sealed class ListOfficeRequestsHandler(
    ICallerContext caller,
    IOfficeRequestRepository requests) : IQueryHandler<ListOfficeRequests, IReadOnlyList<OfficeRequestView>>
{
    public async Task<IReadOnlyList<OfficeRequestView>> Handle(ListOfficeRequests query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var rows = await requests.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(OfficeMaps.View).ToArray();
    }
}

public sealed record ListMyOfficeRequests;

public sealed class ListMyOfficeRequestsHandler(
    ICallerContext caller,
    IOfficeRequestRepository requests) : IQueryHandler<ListMyOfficeRequests, IReadOnlyList<OfficeRequestView>>
{
    public async Task<IReadOnlyList<OfficeRequestView>> Handle(ListMyOfficeRequests query, CancellationToken cancellationToken)
    {
        var userId = caller.UserId;
        var rows = await requests.ListForAuthorAsync(userId, cancellationToken).ConfigureAwait(false);
        return rows.Select(OfficeMaps.View).ToArray();
    }
}

public sealed record SendOfficeRequest(string Message, Guid? GraveSiteId);

public sealed class SendOfficeRequestValidator : AbstractValidator<SendOfficeRequest>
{
    public SendOfficeRequestValidator()
    {
        RuleFor(command => command.Message)
            .Must(text => (text ?? "").Trim().Length is >= 1 and <= OfficeRequest.MaxMessageLength)
            .WithErrorCode("request.message_invalid");
        RuleFor(command => command.GraveSiteId).Must(id => id != Guid.Empty).WithErrorCode("grave_site.not_found");
    }
}

public sealed class SendOfficeRequestHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IOfficeRequestRepository requests,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SendOfficeRequest, OfficeRequestView>
{
    public async Task<OfficeRequestView> Handle(SendOfficeRequest command, CancellationToken cancellationToken)
    {
        var userId = caller.UserId;
        var organizationId = caller.OrganizationId;
        if (command.GraveSiteId is Guid graveSiteId)
        {
            var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
            if (site is null)
                throw new NotFoundException("grave_site.not_found");
        }

        var created = OfficeRequest.Send(organizationId, userId, command.Message, command.GraveSiteId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        await requests.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OfficeMaps.View(created);
    }
}

internal static class OfficeMaps
{
    public static OfficeRequestView View(OfficeRequest request) =>
        new(request.Id, request.Message, request.CreatedOn, request.GraveSiteId);
}
