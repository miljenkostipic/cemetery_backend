using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Organizations;

public sealed record CreateOrganization(string Name);

public sealed record OrganizationCreated(Guid Id, string Name, string Slug, string Role);

public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganization>
{
    public CreateOrganizationValidator()
    {
        RuleFor(command => command.Name)
            .Must(Organization.IsValidName)
            .WithErrorCode("organization.name_invalid");
    }
}

public sealed class CreateOrganizationHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    IActiveOrganizationStore active,
    TimeProvider clock) : ICommandHandler<CreateOrganization, OrganizationCreated>
{
    public async Task<OrganizationCreated> Handle(CreateOrganization command, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var slug = OrganizationSlug.FromName(command.Name);
        if (await organizations.SlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
            throw new ConflictException("organization.slug_taken");

        var organization = Organization.Create(command.Name, slug, clock.GetUtcNow());
        tenant.Use(organization.Id);
        await organizations.AddAsync(organization, cancellationToken).ConfigureAwait(false);
        await memberships.AddAsync(
            Membership.Create(organization.Id, userId, MembershipRole.OrganizationAdmin),
            cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        active.Remember(organization.Id);
        return new OrganizationCreated(organization.Id, organization.Name, organization.Slug, MembershipRoles.OrganizationAdmin);
    }
}
