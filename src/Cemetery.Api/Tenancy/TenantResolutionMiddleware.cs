using Cemetery.Application.Abstractions;
using Cemetery.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
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
        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.TenantId)
            .ToListAsync()
            .ConfigureAwait(false);
        var selected = ReadCookie(context);
        if (selected is Guid organizationId && memberships.Contains(organizationId))
            tenant.Use(organizationId);
        else if (memberships.Count == 1)
            tenant.Use(memberships[0]);
    }

    private static Guid? ReadCookie(HttpContext context) =>
        Guid.TryParse(context.Request.Cookies[CookieOrganizationStore.Name], out var id) ? id : null;
}

public sealed class TenantRequirement : IAuthorizationRequirement;

public sealed class TenantAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<TenantRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        if (tenant.OrganizationId is not null)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
