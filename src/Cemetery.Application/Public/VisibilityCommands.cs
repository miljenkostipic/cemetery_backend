using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Layout;
using Cemetery.Application.Register;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Catalog;

public sealed record PublishRegister(int HideRecentDeathsDays);

public sealed class PublishRegisterValidator : AbstractValidator<PublishRegister>
{
    public PublishRegisterValidator() =>
        RuleFor(command => command.HideRecentDeathsDays)
            .InclusiveBetween(0, PublicListing.HideRecentDeathsMaxDays)
            .WithErrorCode("visibility.days_invalid");
}

public sealed class PublishRegisterHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOrganizationRepository organizations,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<PublishRegister, VisibilityView>
{
    public async Task<VisibilityView> Handle(PublishRegister command, CancellationToken cancellationToken)
    {
        var organization = await OrganizationOfClerkAsync(cancellationToken).ConfigureAwait(false);
        organization.PublishRegister(command.HideRecentDeathsDays);
        await AuditAsync(organization.Id, AuditActions.RegisterPublished, command.HideRecentDeathsDays.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new VisibilityView(organization.PublishesRegister, organization.HideRecentDeathsDays);
    }

    private Task<Domain.Identity.Organization> OrganizationOfClerkAsync(CancellationToken cancellationToken) =>
        ClerkOrganization.RequireAsync(current, tenant, memberships, organizations, cancellationToken);

    private Task AuditAsync(Guid organizationId, string action, string? reason, CancellationToken cancellationToken) =>
        register.AddAuditAsync(AuditEntry.Write(organizationId, action, organizationId, reason, clock.GetUtcNow()), cancellationToken);
}

public sealed record WithdrawRegister();

public sealed class WithdrawRegisterValidator : AbstractValidator<WithdrawRegister>
{
    public WithdrawRegisterValidator()
    {
    }
}

public sealed class WithdrawRegisterHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOrganizationRepository organizations,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<WithdrawRegister, VisibilityView>
{
    public async Task<VisibilityView> Handle(WithdrawRegister command, CancellationToken cancellationToken)
    {
        var organization = await ClerkOrganization.RequireAsync(current, tenant, memberships, organizations, cancellationToken).ConfigureAwait(false);
        organization.WithdrawRegister();
        await register.AddAuditAsync(AuditEntry.Write(organization.Id, AuditActions.RegisterWithdrawn, organization.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new VisibilityView(organization.PublishesRegister, organization.HideRecentDeathsDays);
    }
}

public sealed record HideGraveFromPublic(Guid GraveSiteId);

public sealed class HideGraveFromPublicValidator : AbstractValidator<HideGraveFromPublic>
{
    public HideGraveFromPublicValidator() =>
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class HideGraveFromPublicHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<HideGraveFromPublic, GraveSiteView>
{
    public Task<GraveSiteView> Handle(HideGraveFromPublic command, CancellationToken cancellationToken) =>
        GraveVisibility.HideAsync(current, tenant, memberships, layouts, register, unitOfWork, clock, command.GraveSiteId, cancellationToken);
}

public sealed record ShowGraveOnPublicMap(Guid GraveSiteId);

public sealed class ShowGraveOnPublicMapValidator : AbstractValidator<ShowGraveOnPublicMap>
{
    public ShowGraveOnPublicMapValidator() =>
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class ShowGraveOnPublicMapHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ShowGraveOnPublicMap, GraveSiteView>
{
    public Task<GraveSiteView> Handle(ShowGraveOnPublicMap command, CancellationToken cancellationToken) =>
        GraveVisibility.ShowAsync(current, tenant, memberships, layouts, register, unitOfWork, clock, command.GraveSiteId, cancellationToken);
}

public sealed record DescribeMemorial(Guid DeceasedId, string? Epitaph, string? PhotoUrl);

public sealed class DescribeMemorialValidator : AbstractValidator<DescribeMemorial>
{
    public DescribeMemorialValidator() =>
        RuleFor(command => command.DeceasedId).NotEmpty().WithErrorCode("deceased.not_found");
}

public sealed class DescribeMemorialHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IRegisterRepository register,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DescribeMemorial, DeceasedView>
{
    public async Task<DeceasedView> Handle(DescribeMemorial command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var person = await register.FindDeceasedAsync(command.DeceasedId, cancellationToken).ConfigureAwait(false);
        if (person is null || person.RemovedAt is not null)
            throw new NotFoundException("deceased.not_found");
        person.DescribeMemorial(command.Epitaph, command.PhotoUrl);
        await register.AddAuditAsync(AuditEntry.Write(person.TenantId, AuditActions.MemorialDescribed, person.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return RegisterMaps.ToView(person, null, null);
    }
}

file static class ClerkOrganization
{
    public static async Task<Domain.Identity.Organization> RequireAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        IOrganizationRepository organizations,
        CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        return await organizations.FindAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("organization.not_found");
    }
}

file static class GraveVisibility
{
    public static Task<GraveSiteView> HideAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        Guid graveSiteId,
        CancellationToken cancellationToken) =>
        ChangeAsync(current, tenant, memberships, layouts, register, unitOfWork, clock, graveSiteId, AuditActions.GraveHidden, site => site.HideFromPublic(), cancellationToken);

    public static Task<GraveSiteView> ShowAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        Guid graveSiteId,
        CancellationToken cancellationToken) =>
        ChangeAsync(current, tenant, memberships, layouts, register, unitOfWork, clock, graveSiteId, AuditActions.GraveShown, site => site.ShowOnPublicMap(), cancellationToken);

    private static async Task<GraveSiteView> ChangeAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        Guid graveSiteId,
        string action,
        Action<Domain.Layout.GraveSite> apply,
        CancellationToken cancellationToken)
    {
        var organizationId = await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        apply(site);
        await register.AddAuditAsync(AuditEntry.Write(organizationId, action, site.Id, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(site);
    }
}
