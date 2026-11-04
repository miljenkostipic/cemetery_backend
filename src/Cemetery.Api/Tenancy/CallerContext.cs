using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.Tenancy;

public readonly record struct CallerState(Guid UserId, Guid OrganizationId, MembershipRole Role)
{
    public const string ItemKey = "cemetery.caller";

    public static CallerState? Read(HttpContext? http) =>
        http?.Items[ItemKey] is CallerState state ? state : null;
}

public sealed class HttpCallerContext(IHttpContextAccessor http) : ICallerContext
{
    public Guid UserId => Current.UserId;

    public Guid OrganizationId => Current.OrganizationId;

    public MembershipRole Role => Current.Role;

    private CallerState Current =>
        CallerState.Read(http.HttpContext) ?? throw new ForbiddenException("tenant.required");
}
