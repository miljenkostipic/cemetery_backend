using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Organizations;

public sealed record InviteMember(string Email, string Role);

public sealed record InvitationIssued(Guid Id, string Email);

public sealed class InviteMemberValidator : AbstractValidator<InviteMember>
{
    public InviteMemberValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithErrorCode("auth.email_invalid");
        RuleFor(command => command.Role)
            .Must(role => MembershipRoles.TryParse(role, out _))
            .WithErrorCode("membership.role_invalid");
    }
}

public sealed class InviteMemberHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IInvitationRepository invitations,
    IInvitationTokenFactory tokens,
    IEmailSender email,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<InviteMember, InvitationIssued>
{
    public async Task<InvitationIssued> Handle(InviteMember command, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var organizationId = tenant.OrganizationId ?? throw new ForbiddenException("tenant.required");
        await RequireAdminAsync(userId, cancellationToken).ConfigureAwait(false);
        var issued = await IssueAsync(command, organizationId, cancellationToken).ConfigureAwait(false);
        return issued;
    }

    private async Task RequireAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (role != MembershipRole.OrganizationAdmin)
            throw new ForbiddenException("auth.forbidden");
    }

    private async Task<InvitationIssued> IssueAsync(InviteMember command, Guid organizationId, CancellationToken cancellationToken)
    {
        var address = EmailAddress.Parse(command.Email);
        if (!MembershipRoles.TryParse(command.Role, out var role))
            throw new Exceptions.ValidationFailedException(["membership.role_invalid"]);
        var token = tokens.Create();
        var now = clock.GetUtcNow();
        var invitation = Invitation.Issue(organizationId, address, role, tokens.Hash(token), now, now.Add(Invitation.Lifetime));
        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await email.SendInvitationAsync(address, links.InvitationUrl(token), cancellationToken).ConfigureAwait(false);
        return new InvitationIssued(invitation.Id, address.Value);
    }
}
