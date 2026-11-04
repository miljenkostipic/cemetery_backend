using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;

namespace Cemetery.Application.Dashboard;

public sealed record GetDashboard;

public sealed record DashboardView(Guid OrganizationId, string Name, string Slug, int GraveSiteCount);

public sealed class GetDashboardHandler(
    ICallerContext caller,
    IOrganizationRepository organizations,
    ILayoutRepository layouts) : IQueryHandler<GetDashboard, DashboardView>
{
    public async Task<DashboardView> Handle(GetDashboard query, CancellationToken cancellationToken)
    {
        var organization = await organizations.FindAsync(caller.OrganizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ForbiddenException("tenant.forbidden");
        var graveSiteCount = await layouts.CountGraveSitesAsync(cancellationToken).ConfigureAwait(false);
        return new DashboardView(organization.Id, organization.Name, organization.Slug, graveSiteCount);
    }
}
