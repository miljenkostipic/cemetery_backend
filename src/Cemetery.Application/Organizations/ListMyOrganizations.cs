using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;

namespace Cemetery.Application.Organizations;

public sealed record ListMyOrganizations;

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

public sealed class ListMyOrganizationsHandler(
    ICurrentUser current,
    IOrganizationRepository organizations) : IQueryHandler<ListMyOrganizations, IReadOnlyList<OrganizationSummary>>
{
    public async Task<IReadOnlyList<OrganizationSummary>> Handle(ListMyOrganizations query, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var memberships = await organizations.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return memberships
            .Select(item => new OrganizationSummary(item.OrganizationId, item.Name, item.Slug, MembershipRoles.ToCode(item.Role)))
            .ToArray();
    }
}
