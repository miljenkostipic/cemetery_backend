using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Register;

public sealed record RecordDeceased(Guid CemeteryId, string GivenName, string FamilyName, DateOnly? BornOn, DateOnly? DiedOn);

public sealed class RecordDeceasedValidator : AbstractValidator<RecordDeceased>
{
    public RecordDeceasedValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.GivenName).Must(name => IsName(name)).WithErrorCode("deceased.given_name_invalid");
        RuleFor(command => command.FamilyName).Must(name => IsName(name)).WithErrorCode("deceased.family_name_invalid");
    }

    private static bool IsName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= PersonName.MaxLength;
}

public sealed class RecordDeceasedHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RecordDeceased, DeceasedView>
{
    public async Task<DeceasedView> Handle(RecordDeceased command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        _ = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var person = Deceased.Record(organizationId, command.CemeteryId, command.GivenName, command.FamilyName, command.BornOn, command.DiedOn, clock.GetUtcNow());
        await register.AddDeceasedAsync(person, cancellationToken).ConfigureAwait(false);
        await register.AddAuditAsync(AuditEntry.Write(organizationId, AuditActions.DeceasedRecorded, person.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(person, null, null);
    }
}

public sealed record ListDeceased(Guid CemeteryId);

public sealed class ListDeceasedHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register) : IQueryHandler<ListDeceased, IReadOnlyList<DeceasedView>>
{
    public async Task<IReadOnlyList<DeceasedView>> Handle(ListDeceased query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var people = await register.ListDeceasedAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var interments = await register.ListIntermentsByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return people.Select(person => View(person, interments, sites)).ToArray();
    }

    private static DeceasedView View(Deceased person, IReadOnlyList<Interment> interments, IReadOnlyList<Domain.Layout.GraveSite> sites)
    {
        var buried = RegisterMaps.Occupying(person.Id, interments);
        return RegisterMaps.ToView(person, buried, RegisterMaps.CodeOf(buried?.GraveSiteId, sites));
    }
}

public sealed record GetDeceased(Guid DeceasedId);

public sealed class GetDeceasedHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register) : IQueryHandler<GetDeceased, DeceasedView>
{
    public async Task<DeceasedView> Handle(GetDeceased query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var person = DeceasedPresence.Present(await register.FindDeceasedAsync(query.DeceasedId, cancellationToken).ConfigureAwait(false));
        var interments = await register.ListIntermentsByDeceasedAsync(person.Id, cancellationToken).ConfigureAwait(false);
        var buried = RegisterMaps.Occupying(person.Id, interments);
        var site = buried is null ? null : await layouts.FindGraveSiteAsync(buried.GraveSiteId, cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(person, buried, site?.Code);
    }
}

public sealed record CorrectDeceased(Guid DeceasedId, string GivenName, string FamilyName, DateOnly? BornOn, DateOnly? DiedOn, string Reason);

public sealed class CorrectDeceasedValidator : AbstractValidator<CorrectDeceased>
{
    public CorrectDeceasedValidator()
    {
        RuleFor(command => command.DeceasedId).NotEmpty().WithErrorCode("deceased.not_found");
        RuleFor(command => command.GivenName).Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("deceased.given_name_invalid");
        RuleFor(command => command.FamilyName).Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("deceased.family_name_invalid");
        RuleFor(command => command.Reason).Must(reason => !string.IsNullOrWhiteSpace(reason)).WithErrorCode("deceased.reason_required");
    }
}

public sealed class CorrectDeceasedHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CorrectDeceased, DeceasedView>
{
    public async Task<DeceasedView> Handle(CorrectDeceased command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var person = DeceasedPresence.Present(await register.FindDeceasedAsync(command.DeceasedId, cancellationToken).ConfigureAwait(false));
        await RequireBurialOrderAsync(person, command.DiedOn, cancellationToken).ConfigureAwait(false);
        var revision = person.Correct(command.GivenName, command.FamilyName, command.BornOn, command.DiedOn, command.Reason, clock.GetUtcNow());
        await register.AddRevisionAsync(revision, cancellationToken).ConfigureAwait(false);
        await register.AddAuditAsync(AuditEntry.Write(person.TenantId, AuditActions.DeceasedCorrected, person.Id, command.Reason, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(person, null, null);
    }

    private async Task RequireBurialOrderAsync(Deceased person, DateOnly? diedOn, CancellationToken cancellationToken)
    {
        var history = await register.ListIntermentsByDeceasedAsync(person.Id, cancellationToken).ConfigureAwait(false);
        var buried = RegisterMaps.Occupying(person.Id, history);
        if (buried is not null)
            LifeDates.RequireBurial(diedOn, buried.BuriedOn);
    }
}

public sealed record RemoveDeceased(Guid DeceasedId);

public sealed class RemoveDeceasedValidator : AbstractValidator<RemoveDeceased>
{
    public RemoveDeceasedValidator() =>
        RuleFor(command => command.DeceasedId).NotEmpty().WithErrorCode("deceased.not_found");
}

public sealed class RemoveDeceasedHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RemoveDeceased, bool>
{
    public async Task<bool> Handle(RemoveDeceased command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var person = DeceasedPresence.Present(await register.FindDeceasedAsync(command.DeceasedId, cancellationToken).ConfigureAwait(false));
        await ReleaseAsync(person, cancellationToken).ConfigureAwait(false);
        person.Remove(clock.GetUtcNow());
        await WriteRemovalAsync(person, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ReleaseAsync(Deceased person, CancellationToken cancellationToken)
    {
        var history = await register.ListIntermentsByDeceasedAsync(person.Id, cancellationToken).ConfigureAwait(false);
        foreach (var interment in history)
            await ReleaseOneAsync(person, interment, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseOneAsync(Deceased person, Interment interment, CancellationToken cancellationToken)
    {
        if (!interment.Occupies)
            return;
        var today = RegisterClock.Today(clock);
        interment.Exhume(interment.BuriedOn > today ? interment.BuriedOn : today);
        await register.AddAuditAsync(AuditEntry.Write(person.TenantId, AuditActions.IntermentExhumed, interment.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }

    private Task WriteRemovalAsync(Deceased person, CancellationToken cancellationToken) =>
        register.AddAuditAsync(AuditEntry.Write(person.TenantId, AuditActions.DeceasedRemoved, person.Id, null, clock.GetUtcNow()), cancellationToken);
}

file static class DeceasedPresence
{
    public static Deceased Present(Deceased? person)
    {
        if (person is null || person.RemovedAt is not null)
            throw new NotFoundException("deceased.not_found");
        return person;
    }
}

public sealed record SetRestPeriod(Guid CemeteryId, int Years);

public sealed class SetRestPeriodValidator : AbstractValidator<SetRestPeriod>
{
    public SetRestPeriodValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.Years).InclusiveBetween(RestPeriod.MinimumYears, RestPeriod.MaximumYears).WithErrorCode("cemetery.rest_period_invalid");
    }
}

public sealed class SetRestPeriodHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SetRestPeriod, int>
{
    public async Task<int> Handle(SetRestPeriod command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        cemetery.SetRestPeriod(command.Years);
        await register.AddAuditAsync(AuditEntry.Write(organizationId, AuditActions.RestPeriodSet, cemetery.Id, command.Years.ToString(System.Globalization.CultureInfo.InvariantCulture), clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return cemetery.RestPeriodYears;
    }
}
