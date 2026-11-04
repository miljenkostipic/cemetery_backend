using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Application;

internal static class Require
{
    public static async Task CemeteryAsync(ILayoutRepository layouts, Guid cemeteryId, CancellationToken cancellationToken)
    {
        _ = await layouts.FindCemeteryAsync(cemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
    }

    public static async Task<GraveSite> SiteAsync(
        ILayoutRepository layouts,
        Guid cemeteryId,
        Guid graveSiteId,
        string code,
        CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
        if (site is null || site.CemeteryId != cemeteryId)
            throw new DomainRuleException(code);

        return site;
    }

    public static async Task<Deceased> PersonAsync(
        IRegisterRepository register,
        Guid cemeteryId,
        Guid deceasedId,
        CancellationToken cancellationToken)
    {
        var person = await register.FindDeceasedAsync(deceasedId, cancellationToken).ConfigureAwait(false);
        if (person is null || person.RemovedAt is not null || person.CemeteryId != cemeteryId)
            throw new DomainRuleException("funeral.deceased_invalid");

        return person;
    }
}
