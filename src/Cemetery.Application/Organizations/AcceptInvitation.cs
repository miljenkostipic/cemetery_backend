using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Organizations;

public sealed record AcceptInvitation(string Token);

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitation>
{
    public AcceptInvitationValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .WithErrorCode("invitation.invalid");
    }
}

public sealed class AcceptInvitationHandler(
    ICurrentUser current,
    ITenantContext tenant,
    ISessionHints hints,
    IInvitationTokenFactory tokens,
    IInvitationRepository invitations,
    IMembershipRepository memberships,
    IOrganizationRepository organizations,
    IUnitOfWork unitOfWork,
    IActiveOrganizationStore active,
    TimeProvider clock) : ICommandHandler<AcceptInvitation, OrganizationSummary>
{
    public async Task<OrganizationSummary> Handle(AcceptInvitation command, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var email = EmailAddress.Parse(current.Email ?? throw new UnauthorizedException("auth.required"));
        var invitation = await FindAsync(command.Token, cancellationToken).ConfigureAwait(false);
        invitation.Accept(email, clock.GetUtcNow());
        await AddMembershipAsync(invitation, userId, cancellationToken).ConfigureAwait(false);
        tenant.Use(invitation.TenantId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        active.Remember(invitation.TenantId);
        var organization = await organizations.FindAsync(invitation.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("organization.not_found");
        return new OrganizationSummary(organization.Id, organization.Name, organization.Slug, MembershipRoles.ToCode(invitation.Role));
    }

    private async Task<Invitation> FindAsync(string token, CancellationToken cancellationToken)
    {
        hints.InvitationHash = tokens.Hash(token);
        return await invitations.FindByHashAsync(hints.InvitationHash, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("invitation.not_found");
    }

    private async Task AddMembershipAsync(Invitation invitation, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await memberships.FindForUserAsync(invitation.TenantId, userId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            await memberships.AddAsync(Membership.Create(invitation.TenantId, userId, invitation.Role), cancellationToken).ConfigureAwait(false);
    }
}
