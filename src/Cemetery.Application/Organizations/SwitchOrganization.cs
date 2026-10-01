using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Organizations;

public sealed record SwitchOrganization(Guid OrganizationId);

public sealed class SwitchOrganizationValidator : AbstractValidator<SwitchOrganization>
{
    public SwitchOrganizationValidator()
    {
        RuleFor(command => command.OrganizationId)
            .Must(id => id != Guid.Empty)
            .WithErrorCode("organization.invalid");
    }
}

public sealed class SwitchOrganizationHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IOrganizationRepository organizations,
    IActiveOrganizationStore active) : ICommandHandler<SwitchOrganization, OrganizationSummary>
{
    public async Task<OrganizationSummary> Handle(SwitchOrganization command, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var mine = await organizations.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var match = mine.FirstOrDefault(item => item.OrganizationId == command.OrganizationId)
            ?? throw new ForbiddenException("tenant.forbidden");
        tenant.Use(match.OrganizationId);
        active.Remember(match.OrganizationId);
        return new OrganizationSummary(match.OrganizationId, match.Name, match.Slug, MembershipRoles.ToCode(match.Role));
    }
}
