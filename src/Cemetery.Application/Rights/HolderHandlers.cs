using Cemetery.Application.Abstractions;
using Cemetery.Application.Organizations;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record ListHolders(string? Query, string? Cursor, int? Limit);

public sealed class ListHoldersHandler(ICallerContext caller, IRightsRepository rights) : IQueryHandler<ListHolders, Page<HolderRecordView>>
{
    public async Task<Page<HolderRecordView>> Handle(ListHolders query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var page = await rights.PageHoldersAsync(query.Query, new PageRequest(query.Cursor, query.Limit), cancellationToken).ConfigureAwait(false);
        return new Page<HolderRecordView>(page.Items.Select(RightsMaps.Record).ToArray(), page.NextCursor);
    }
}

public sealed record GetHolder(Guid HolderId);

public sealed class GetHolderHandler(ICallerContext caller, IRightsRepository rights) : IQueryHandler<GetHolder, HolderRecordView>
{
    public async Task<HolderRecordView> Handle(GetHolder query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var holder = await rights.FindHolderAsync(query.HolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        return RightsMaps.Record(holder);
    }
}

public sealed record GetMyHolder;

public sealed class GetMyHolderHandler(ICallerContext caller, IRightsRepository rights) : IQueryHandler<GetMyHolder, HolderRecordView>
{
    public async Task<HolderRecordView> Handle(GetMyHolder query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        return RightsMaps.Record(holder);
    }
}

public sealed record CreateHolder(string GivenName, string FamilyName, string? Oib, string? Address, string? Email, string? Phone);

public sealed class CreateHolderValidator : AbstractValidator<CreateHolder>
{
    public CreateHolderValidator()
    {
        RuleFor(command => command.GivenName).NotEmpty().WithErrorCode("deceased.given_name_invalid");
        RuleFor(command => command.FamilyName).NotEmpty().WithErrorCode("deceased.family_name_invalid");
    }
}

public sealed class CreateHolderHandler(ICallerContext caller, IRightsRepository rights, IUnitOfWork unitOfWork) : ICommandHandler<CreateHolder, HolderRecordView>
{
    public async Task<HolderRecordView> Handle(CreateHolder command, CancellationToken cancellationToken)
    {
        var holder = Holder.Register(caller.OrganizationId, command.GivenName, command.FamilyName, command.Oib, command.Address, command.Email, command.Phone);
        await rights.AddHolderAsync(holder, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RightsMaps.Record(holder);
    }
}

public sealed record UpdateHolder(Guid HolderId, string GivenName, string FamilyName, string? Oib, string? Address, string? Email, string? Phone);

public sealed class UpdateHolderValidator : AbstractValidator<UpdateHolder>
{
    public UpdateHolderValidator()
    {
        RuleFor(command => command.HolderId).NotEmpty().WithErrorCode("holder.not_found");
        RuleFor(command => command.GivenName).NotEmpty().WithErrorCode("deceased.given_name_invalid");
        RuleFor(command => command.FamilyName).NotEmpty().WithErrorCode("deceased.family_name_invalid");
    }
}

public sealed class UpdateHolderHandler(ICallerContext caller, IRightsRepository rights, IUnitOfWork unitOfWork) : ICommandHandler<UpdateHolder, HolderRecordView>
{
    public async Task<HolderRecordView> Handle(UpdateHolder command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var holder = await rights.FindHolderAsync(command.HolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        holder.Correct(command.GivenName, command.FamilyName, command.Oib, command.Address, command.Email, command.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RightsMaps.Record(holder);
    }
}

public sealed record InviteHolder(Guid HolderId);

public sealed class InviteHolderValidator : AbstractValidator<InviteHolder>
{
    public InviteHolderValidator() => RuleFor(command => command.HolderId).NotEmpty().WithErrorCode("holder.not_found");
}

public sealed class InviteHolderHandler(
    ICallerContext caller,
    IRightsRepository rights,
    IInvitationRepository invitations,
    IInvitationTokenFactory tokens,
    IEmailSender email,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<InviteHolder, InvitationIssued>
{
    public async Task<InvitationIssued> Handle(InviteHolder command, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderAsync(command.HolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("holder.not_found");
        if (holder.Email is null)
            throw new ValidationFailedException(["auth.email_invalid"]);

        return await IssueAsync(holder.Email, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InvitationIssued> IssueAsync(string address, CancellationToken cancellationToken)
    {
        var emailAddress = EmailAddress.Parse(address);
        var token = tokens.Create();
        var now = clock.GetUtcNow();
        var invitation = Invitation.Issue(caller.OrganizationId, emailAddress, MembershipRole.GraveHolder, tokens.Hash(token), now, now.Add(Invitation.Lifetime));
        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await email.SendInvitationAsync(emailAddress, links.InvitationUrl(token), cancellationToken).ConfigureAwait(false);
        return new InvitationIssued(invitation.Id, emailAddress.Value);
    }
}
