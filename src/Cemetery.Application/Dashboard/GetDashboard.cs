using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;

namespace Cemetery.Application.Dashboard;

public sealed record GetDashboard;

public sealed record DashboardView(Guid OrganizationId, string Name, string Slug, int GraveSiteCount);

public sealed class GetDashboardHandler(
    ITenantContext tenant,
    IOrganizationRepository organizations) : IQueryHandler<GetDashboard, DashboardView>
{
    public const int EmptyGraveSiteCount = 0;

    public async Task<DashboardView> Handle(GetDashboard query, CancellationToken cancellationToken)
    {
        var organizationId = tenant.OrganizationId ?? throw new ForbiddenException("tenant.required");
        var organization = await organizations.FindAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ForbiddenException("tenant.forbidden");
        return new DashboardView(organization.Id, organization.Name, organization.Slug, EmptyGraveSiteCount);
    }
}
