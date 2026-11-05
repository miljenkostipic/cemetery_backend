using Cemetery.Domain.Rights;

namespace Cemetery.Application.Rights;

public sealed record HolderRecordBody(string GivenName, string FamilyName, string? Oib, string? Address, string? Email, string? Phone);

public sealed record HolderRecordView(Guid Id, string GivenName, string FamilyName, string? Oib, string? Address, string? Email, string? Phone, Guid? UserId);

public sealed record HolderBody(string GivenName, string FamilyName, string Email, int Share);

public sealed record HolderView(Guid Id, string GivenName, string FamilyName, string Email, int Share);

public sealed record GraveRightView(Guid Id, Guid GraveSiteId, string Status, bool Perpetual, DateOnly StartsOn, DateOnly? EndsOn, IReadOnlyList<HolderView> Holders);

public sealed record GrantRightBody(bool Perpetual, int? TermYears, IReadOnlyList<HolderBody> Holders);

public sealed record RenewRightBody(int Years);

public sealed record ConveyRightBody(DateOnly On, IReadOnlyList<HolderBody> Holders);

public sealed record ReservationBody(string GivenName, string FamilyName, string Email, DateOnly Until);

public sealed record ReservationView(Guid Id, Guid GraveSiteId, string GivenName, string FamilyName, string Email, DateOnly Until);

public sealed record SiteRightsView(GraveRightView? Right, ReservationView? Reservation);

public sealed record DocumentBody(string FileName, string ContentType, string ContentBase64, string? Kind);

public sealed record DocumentView(Guid Id, Guid GraveSiteId, string FileName, string ContentType, int ByteSize);

public sealed record HolderSiteView(Guid GraveSiteId, Guid CemeteryId, string CemeteryName, string Code, int Share, bool Perpetual, DateOnly? EndsOn);

public sealed record FileDownload(byte[] Content, string ContentType, string FileName);

internal static class RightsMaps
{
    public static HolderRecordView Record(Holder holder) =>
        new(holder.Id, holder.GivenName, holder.FamilyName, holder.Oib, holder.Address, holder.Email, holder.Phone, holder.UserId);

    public static string Status(GraveRightStatus status) => status == GraveRightStatus.Active ? "active" : "ended";
}
