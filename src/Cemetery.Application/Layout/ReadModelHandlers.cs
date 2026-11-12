using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Operations;
using Cemetery.Application.Photos;
using Cemetery.Application.Register;
using Cemetery.Application.Rights;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Rights;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Layout;

public sealed record MapDocument(string Etag, string Json);

public sealed record GetCemeteryMap(Guid CemeteryId, string? IfNoneMatch);

public sealed class GetCemeteryMapHandler(ICallerContext caller, ILayoutRepository layouts, ICemeteryReadModel read) : IQueryHandler<GetCemeteryMap, MapDocument?>
{
    public async Task<MapDocument?> Handle(GetCemeteryMap query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var (etag, json) = await read.MapAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        if (query.IfNoneMatch is not null && Same(query.IfNoneMatch, etag))
            return null;
        return new MapDocument(etag, json);
    }

    private static bool Same(string header, string etag)
    {
        var expected = Token(etag);
        foreach (var part in header.Split(','))
        {
            if (Token(part) == expected)
                return true;
        }

        return false;
    }

    private static string Token(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("W/", StringComparison.Ordinal))
            text = text[2..].Trim();
        return text.Trim('"');
    }
}

public sealed record DossierSlot(int Position, string State);

public sealed record DossierBurial(string Name, int? BornYear, int? DiedYear, DateOnly BuriedOn);

public sealed record DossierHolder(string Name, string? Email, string? Phone);

public sealed record DossierRight(Guid Id, string Status, IReadOnlyList<DossierHolder> Holders);

public sealed record DossierOrder(Guid Id, string Kind, string Status);

public sealed record DossierView(
    GraveSiteView Site,
    IReadOnlyList<DossierSlot> Slots,
    IReadOnlyList<DossierBurial> Interments,
    DossierRight? Right,
    string? FeeStatus,
    string? PrimaryPhotoUrl,
    IReadOnlyList<DossierOrder> OpenWorkOrders);

public sealed record GetDossier(Guid GraveSiteId);

public sealed class GetDossierHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IRightsRepository rights,
    IBillingRepository billing,
    ICemeteryReadModel read,
    IGraveSitePhotoRepository photos,
    TimeProvider clock) : IQueryHandler<GetDossier, DossierView>
{
    public async Task<DossierView> Handle(GetDossier query, CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var cemetery = await layouts.FindCemeteryAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var interments = await register.ListIntermentsBySiteAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var names = await Names(interments, cancellationToken).ConfigureAwait(false);
        return await Compose(site, cemetery, interments, names, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DossierView> Compose(GraveSite site, CemeteryPlace cemetery, IReadOnlyList<Interment> interments, IReadOnlyDictionary<Guid, Deceased> names, CancellationToken cancellationToken)
    {
        var today = RegisterClock.Today(clock);
        var right = await rights.FindActiveRightAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var photo = await PrimaryPhoto(site.Id, cancellationToken).ConfigureAwait(false);
        var orders = await read.ListOpenWorkOrdersAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var fee = await Fee(site, today, cancellationToken).ConfigureAwait(false);
        return new DossierView(LayoutMaps.ToView(site), Slots(site, interments, cemetery, today), Burials(interments, names), await Holders(right, cancellationToken).ConfigureAwait(false), fee, photo, orders.Select(Order).ToArray());
    }

    private async Task<IReadOnlyDictionary<Guid, Deceased>> Names(IReadOnlyList<Interment> interments, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, Deceased>();
        foreach (var interment in interments)
        {
            if (names.ContainsKey(interment.DeceasedId))
                continue;
            var person = await register.FindDeceasedAsync(interment.DeceasedId, cancellationToken).ConfigureAwait(false);
            if (person is not null)
                names[person.Id] = person;
        }

        return names;
    }

    private static DossierSlot[] Slots(GraveSite site, IReadOnlyList<Interment> interments, CemeteryPlace cemetery, DateOnly today)
    {
        var slots = new List<DossierSlot>();
        for (var position = 1; position <= site.Capacity; position++)
            slots.Add(new DossierSlot(position, PositionStates.ToCode(BurialPositions.State(position, interments, today, cemetery.RestPeriodYears))));
        return slots.ToArray();
    }

    private static DossierBurial[] Burials(IReadOnlyList<Interment> interments, IReadOnlyDictionary<Guid, Deceased> names)
    {
        var rows = new List<DossierBurial>();
        foreach (var interment in interments.Where(item => item.Occupies))
        {
            names.TryGetValue(interment.DeceasedId, out var person);
            rows.Add(new DossierBurial(person is null ? "" : person.GivenName + " " + person.FamilyName, person?.BornOn?.Year, person?.DiedOn?.Year, interment.BuriedOn));
        }

        return rows.ToArray();
    }

    private async Task<DossierRight?> Holders(GraveRight? right, CancellationToken cancellationToken)
    {
        if (right is null)
            return null;
        var holders = new List<DossierHolder>();
        var contact = caller.Role is MembershipRole.OrganizationAdmin or MembershipRole.CemeteryClerk;
        foreach (var share in right.Shares)
            holders.Add(await Holder(share.HolderId, contact, cancellationToken).ConfigureAwait(false));
        return new DossierRight(right.Id, RightsMaps.Status(right.Status), holders);
    }

    private async Task<DossierHolder> Holder(Guid holderId, bool contact, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderAsync(holderId, cancellationToken).ConfigureAwait(false);
        if (holder is null)
            return new DossierHolder("", null, null);
        return new DossierHolder(holder.DisplayName, contact ? holder.Email : null, contact ? holder.Phone : null);
    }

    private async Task<string?> Fee(GraveSite site, DateOnly today, CancellationToken cancellationToken)
    {
        var charges = await billing.ListChargesAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false);
        var charge = charges.FirstOrDefault(item => item.GraveSiteId == site.Id && item.Year == today.Year);
        return charge is null ? null : charge.Status.ToString().ToLowerInvariant();
    }

    /// <summary>The grave's cover photo when it has one, otherwise the latest completed work-order photo.</summary>
    private async Task<string?> PrimaryPhoto(Guid siteId, CancellationToken cancellationToken)
    {
        var cover = await photos.FindCoverAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (cover is not null)
            return GraveSitePhotoMaps.ImageUrl(cover.Id);
        var photo = await read.LatestPhotoAsync(siteId, cancellationToken).ConfigureAwait(false);
        return Photo(photo);
    }

    private static string? Photo(WorkOrderPhoto? photo) =>
        photo is null ? null : "/api/v1/work-orders/" + photo.WorkOrderId + "/photos/" + photo.Id;

    private static DossierOrder Order(WorkOrder order) => new(order.Id, WorkKinds.ToCode(order.Kind), WorkOrderStatuses.ToCode(order.Status));
}

public sealed record SearchOffice(string Query, Guid? CemeteryId);

public sealed class SearchOfficeValidator : AbstractValidator<SearchOffice>
{
    public SearchOfficeValidator() => RuleFor(command => command.Query).MinimumLength(2).WithErrorCode("search.query_invalid");
}

public sealed class SearchOfficeHandler(ICallerContext caller, ICemeteryReadModel read) : IQueryHandler<SearchOffice, IReadOnlyList<SearchHit>>
{
    public Task<IReadOnlyList<SearchHit>> Handle(SearchOffice query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        if (query.Query.Trim().Length < 2)
            throw new DomainRuleException("search.query_invalid");
        var holders = caller.Role is MembershipRole.OrganizationAdmin or MembershipRole.CemeteryClerk;
        return read.SearchAsync(query.Query.Trim(), query.CemeteryId, holders, cancellationToken);
    }
}

public sealed record EntranceBody(string Name, double Longitude, double Latitude);

public sealed record EntranceView(Guid Id, Guid CemeteryId, string Name, double Longitude, double Latitude);

public sealed record ListEntrances(Guid CemeteryId);

public sealed class ListEntrancesHandler(ICallerContext caller, ILayoutRepository layouts, ICemeteryReadModel read) : IQueryHandler<ListEntrances, IReadOnlyList<EntranceView>>
{
    public async Task<IReadOnlyList<EntranceView>> Handle(ListEntrances query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var rows = await read.ListEntrancesAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    internal static EntranceView Map(Entrance entrance) => new(entrance.Id, entrance.CemeteryId, entrance.Name, entrance.Location.Longitude, entrance.Location.Latitude);
}

public sealed record PlaceEntrance(Guid CemeteryId, string Name, double Longitude, double Latitude);

public sealed class PlaceEntranceValidator : AbstractValidator<PlaceEntrance>
{
    public PlaceEntranceValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.Name).NotEmpty().WithErrorCode("entrance.name_invalid");
    }
}

public sealed class PlaceEntranceHandler(ICallerContext caller, ILayoutRepository layouts, ICemeteryReadModel read, IUnitOfWork unitOfWork) : ICommandHandler<PlaceEntrance, EntranceView>
{
    public async Task<EntranceView> Handle(PlaceEntrance command, CancellationToken cancellationToken)
    {
        _ = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var entrance = Entrance.Place(caller.OrganizationId, command.CemeteryId, command.Name, new GeoPoint(command.Longitude, command.Latitude));
        await read.AddEntranceAsync(entrance, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ListEntrancesHandler.Map(entrance);
    }
}

public sealed record RemoveEntrance(Guid EntranceId);

public sealed class RemoveEntranceValidator : AbstractValidator<RemoveEntrance>
{
    public RemoveEntranceValidator() => RuleFor(command => command.EntranceId).NotEmpty().WithErrorCode("entrance.not_found");
}

public sealed class RemoveEntranceHandler(ICallerContext caller, ICemeteryReadModel read, IUnitOfWork unitOfWork) : ICommandHandler<RemoveEntrance, bool>
{
    public async Task<bool> Handle(RemoveEntrance command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var entrance = await read.FindEntranceAsync(command.EntranceId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("entrance.not_found");
        await read.RemoveEntranceAsync(entrance, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed record PathBody(string Name, IReadOnlyList<GeoPointView> Line);

public sealed record PathView(Guid Id, Guid CemeteryId, string Name, IReadOnlyList<GeoPointView> Line);

public sealed record ListPaths(Guid CemeteryId);

public sealed class ListPathsHandler(ICallerContext caller, ILayoutRepository layouts, ICemeteryReadModel read) : IQueryHandler<ListPaths, IReadOnlyList<PathView>>
{
    public async Task<IReadOnlyList<PathView>> Handle(ListPaths query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var rows = await read.ListPathsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    internal static PathView Map(CemeteryPath path) =>
        new(path.Id, path.CemeteryId, path.Name, path.Line.Points.Select(point => new GeoPointView(point.Longitude, point.Latitude)).ToArray());
}

public sealed record DrawPath(Guid CemeteryId, string Name, IReadOnlyList<GeoPointView> Line);

public sealed class DrawPathValidator : AbstractValidator<DrawPath>
{
    public DrawPathValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.Name).NotEmpty().WithErrorCode("path.name_invalid");
        RuleFor(command => command.Line).NotEmpty().WithErrorCode("path.line_invalid");
    }
}

public sealed class DrawPathHandler(ICallerContext caller, ILayoutRepository layouts, ICemeteryReadModel read, IUnitOfWork unitOfWork) : ICommandHandler<DrawPath, PathView>
{
    public async Task<PathView> Handle(DrawPath command, CancellationToken cancellationToken)
    {
        _ = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var path = CemeteryPath.Draw(caller.OrganizationId, command.CemeteryId, command.Name, GeoLine.Create(command.Line.Select(point => new GeoPoint(point.Longitude, point.Latitude)).ToArray()));
        await read.AddPathAsync(path, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ListPathsHandler.Map(path);
    }
}

public sealed record RemovePath(Guid PathId);

public sealed class RemovePathValidator : AbstractValidator<RemovePath>
{
    public RemovePathValidator() => RuleFor(command => command.PathId).NotEmpty().WithErrorCode("path.not_found");
}

public sealed class RemovePathHandler(ICallerContext caller, ICemeteryReadModel read, IUnitOfWork unitOfWork) : ICommandHandler<RemovePath, bool>
{
    public async Task<bool> Handle(RemovePath command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var path = await read.FindPathAsync(command.PathId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("path.not_found");
        await read.RemovePathAsync(path, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed record GetWorkOrderPhoto(Guid WorkOrderId, Guid PhotoId);

public sealed class GetWorkOrderPhotoHandler(ICallerContext caller, ICemeteryReadModel read) : IQueryHandler<GetWorkOrderPhoto, FileDownload>
{
    public async Task<FileDownload> Handle(GetWorkOrderPhoto query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var photo = await read.FindPhotoAsync(query.WorkOrderId, query.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        return new FileDownload(photo.Content, photo.ContentType, "photo");
    }
}
