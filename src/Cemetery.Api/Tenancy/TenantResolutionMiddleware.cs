using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, CemeteryDbContext db, ICurrentUser current)
    {
        if (current.UserId is Guid userId)
            await ResolveAsync(context, tenant, db, userId).ConfigureAwait(false);

        await next(context).ConfigureAwait(false);
    }

    private static async Task ResolveAsync(HttpContext context, ITenantContext tenant, CemeteryDbContext db, Guid userId)
    {
        var memberships = await LoadAsync(db, userId).ConfigureAwait(false);
        var selected = Choose(memberships, ReadCookie(context));
        if (selected is MembershipChoice choice)
            Remember(context, tenant, userId, choice);
    }

    private static async Task<List<MembershipChoice>> LoadAsync(CemeteryDbContext db, Guid userId) =>
        await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == userId)
            .Select(membership => new MembershipChoice(membership.TenantId, membership.Role))
            .ToListAsync()
            .ConfigureAwait(false);

    private static MembershipChoice? Choose(IReadOnlyList<MembershipChoice> memberships, Guid? cookie) =>
        Match(memberships, cookie) ?? Single(memberships);

    private static MembershipChoice? Match(IReadOnlyList<MembershipChoice> memberships, Guid? cookie)
    {
        if (cookie is not Guid organizationId)
            return null;

        foreach (var membership in memberships)
        {
            if (membership.OrganizationId == organizationId)
                return membership;
        }

        return null;
    }

    private static MembershipChoice? Single(IReadOnlyList<MembershipChoice> memberships) =>
        memberships.Count == 1 ? memberships[0] : null;

    private static void Remember(HttpContext context, ITenantContext tenant, Guid userId, MembershipChoice choice)
    {
        tenant.Use(choice.OrganizationId);
        context.Items[CallerState.ItemKey] = new CallerState(userId, choice.OrganizationId, choice.Role);
    }

    private static Guid? ReadCookie(HttpContext context) =>
        Guid.TryParse(context.Request.Cookies[CookieOrganizationStore.Name], out var id) ? id : null;

    private readonly record struct MembershipChoice(Guid OrganizationId, MembershipRole Role);
}
