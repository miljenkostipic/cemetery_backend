using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Launch;

public sealed record MaticaLinkView(Guid Id, Guid IntermentId, string DeceasedName, string GraveSiteCode, Guid DeathCertificateId);

public sealed record MaticaLinkBody(Guid DeathCertificateId);

public sealed record ListMaticaLinks(Guid CemeteryId);

public sealed class ListMaticaLinksHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    IMaticaLinkRepository links) : IQueryHandler<ListMaticaLinks, IReadOnlyList<MaticaLinkView>>
{
    public async Task<IReadOnlyList<MaticaLinkView>> Handle(ListMaticaLinks query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        _ = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var rows = await links.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var interments = await register.ListIntermentsByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var people = await register.ListDeceasedAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => MaticaMaps.View(row, interments, people, sites)).ToArray();
    }
}

public sealed record ListCemeteryInterments(Guid CemeteryId);

public sealed class ListCemeteryIntermentsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register) : IQueryHandler<ListCemeteryInterments, IReadOnlyList<IntermentView>>
{
    public async Task<IReadOnlyList<IntermentView>> Handle(ListCemeteryInterments query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        _ = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var interments = await register.ListIntermentsByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var people = await register.ListDeceasedAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return MaticaMaps.Interments(interments, people, sites);
    }
}

public sealed record LinkDeathCertificate(Guid IntermentId, Guid DeathCertificateId);

public sealed class LinkDeathCertificateValidator : AbstractValidator<LinkDeathCertificate>
{
    public LinkDeathCertificateValidator()
    {
        RuleFor(command => command.IntermentId).NotEmpty().WithErrorCode("matica.interment_invalid");
        RuleFor(command => command.DeathCertificateId).NotEmpty().WithErrorCode("matica.certificate_invalid");
    }
}

public sealed class LinkDeathCertificateHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IRegisterRepository register,
    ILayoutRepository layouts,
    IMaticaLinkRepository links,
    IUnitOfWork unitOfWork) : ICommandHandler<LinkDeathCertificate, MaticaLinkView>
{
    public async Task<MaticaLinkView> Handle(LinkDeathCertificate command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var interment = await RequireIntermentAsync(command.IntermentId, cancellationToken).ConfigureAwait(false);
        await RejectDuplicateAsync(command, cancellationToken).ConfigureAwait(false);
        return await SaveAsync(organizationId, interment, command.DeathCertificateId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Interment> RequireIntermentAsync(Guid intermentId, CancellationToken cancellationToken)
    {
        var interment = await register.FindIntermentAsync(intermentId, cancellationToken).ConfigureAwait(false);
        if (interment is null)
            throw new DomainRuleException("matica.interment_invalid");

        return interment;
    }

    private async Task RejectDuplicateAsync(LinkDeathCertificate command, CancellationToken cancellationToken)
    {
        var byInterment = await links.FindByIntermentAsync(command.IntermentId, cancellationToken).ConfigureAwait(false);
        var byCertificate = await links.FindByCertificateAsync(command.DeathCertificateId, cancellationToken).ConfigureAwait(false);
        if (byInterment is not null || byCertificate is not null)
            throw new ConflictException("matica.already_linked");
    }

    private async Task<MaticaLinkView> SaveAsync(Guid organizationId, Interment interment, Guid certificateId, CancellationToken cancellationToken)
    {
        var person = await register.FindDeceasedAsync(interment.DeceasedId, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(interment.GraveSiteId, cancellationToken).ConfigureAwait(false);
        var link = MaticaLink.Create(organizationId, interment.CemeteryId, interment.Id, certificateId);
        await links.AddAsync(link, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MaticaMaps.View(link, person, site?.Code ?? "");
    }
}

internal static class MaticaMaps
{
    public static MaticaLinkView View(
        MaticaLink link,
        IReadOnlyList<Interment> interments,
        IReadOnlyList<Deceased> people,
        IReadOnlyList<Domain.Layout.GraveSite> sites)
    {
        var interment = FindInterment(link.IntermentId, interments);
        var person = interment is null ? null : FindPerson(interment.DeceasedId, people);
        var code = interment is null ? "" : RegisterMaps.CodeOf(interment.GraveSiteId, sites) ?? "";
        return View(link, person, code);
    }

    public static MaticaLinkView View(MaticaLink link, Deceased? person, string graveSiteCode) =>
        new(link.Id, link.IntermentId, Name(person), graveSiteCode, link.DeathCertificateId);

    public static IntermentView[] Interments(IReadOnlyList<Interment> interments, IReadOnlyList<Deceased> people, IReadOnlyList<Domain.Layout.GraveSite> sites)
    {
        var views = new List<IntermentView>(interments.Count);
        foreach (var interment in interments)
            Add(views, interment, people, sites);

        return views.ToArray();
    }

    private static void Add(List<IntermentView> views, Interment interment, IReadOnlyList<Deceased> people, IReadOnlyList<Domain.Layout.GraveSite> sites)
    {
        var person = FindPerson(interment.DeceasedId, people);
        if (person is null)
            return;

        views.Add(RegisterMaps.ToView(interment, person, RegisterMaps.CodeOf(interment.GraveSiteId, sites) ?? ""));
    }

    private static Interment? FindInterment(Guid intermentId, IReadOnlyList<Interment> interments)
    {
        foreach (var interment in interments)
        {
            if (interment.Id == intermentId)
                return interment;
        }

        return null;
    }

    private static Deceased? FindPerson(Guid deceasedId, IReadOnlyList<Deceased> people)
    {
        foreach (var person in people)
        {
            if (person.Id == deceasedId)
                return person;
        }

        return null;
    }

    private static string Name(Deceased? person) =>
        person is null ? "" : $"{person.GivenName} {person.FamilyName}".Trim();
}
