using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record ListMyGraveSites;

public sealed class ListMyGraveSitesHandler(ICallerContext caller, ILayoutRepository layouts, IRightsRepository rights) : IQueryHandler<ListMyGraveSites, IReadOnlyList<HolderSiteView>>
{
    public async Task<IReadOnlyList<HolderSiteView>> Handle(ListMyGraveSites query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false);
        if (holder is null)
            return [];

        var owned = await rights.ListRightsForHolderAsync(holder.Id, cancellationToken).ConfigureAwait(false);
        var views = new List<HolderSiteView>();
        foreach (var right in owned)
            await Add(views, holder.Id, right, cancellationToken).ConfigureAwait(false);
        return views;
    }

    private async Task Add(List<HolderSiteView> views, Guid holderId, GraveRight right, CancellationToken cancellationToken)
    {
        if (right.Status != GraveRightStatus.Active)
            return;
        var site = await layouts.FindGraveSiteAsync(right.GraveSiteId, cancellationToken).ConfigureAwait(false);
        var cemetery = site is null ? null : await layouts.FindCemeteryAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false);
        var share = right.Shares.FirstOrDefault(item => item.HolderId == holderId);
        if (site is null || cemetery is null || share is null)
            return;
        views.Add(new HolderSiteView(site.Id, cemetery.Id, cemetery.Name, site.Code, share.Percent, right.Perpetual, right.ValidTo));
    }
}

public sealed record ListMyDocuments;

public sealed class ListMyDocumentsHandler(ICallerContext caller, IRightsRepository rights) : IQueryHandler<ListMyDocuments, IReadOnlyList<DocumentView>>
{
    public async Task<IReadOnlyList<DocumentView>> Handle(ListMyDocuments query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false);
        if (holder is null)
            return [];

        var owned = await rights.ListRightsForHolderAsync(holder.Id, cancellationToken).ConfigureAwait(false);
        var siteIds = owned.Where(right => right.Status == GraveRightStatus.Active).Select(right => right.GraveSiteId).ToArray();
        var documents = await rights.ListDocumentsForSitesAsync(siteIds, cancellationToken).ConfigureAwait(false);
        return documents.Select(document => new DocumentView(document.Id, document.GraveSiteId, document.FileName, document.ContentType, document.ByteSize)).ToArray();
    }
}

public sealed record ListMyCharges;

public sealed class ListMyChargesHandler(ICallerContext caller, IRightsRepository rights, IBillingRepository billing) : IQueryHandler<ListMyCharges, IReadOnlyList<ChargeView>>
{
    public async Task<IReadOnlyList<ChargeView>> Handle(ListMyCharges query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false);
        if (holder is null)
            return [];

        var charges = await billing.ListChargesForHolderAsync(holder.Id, cancellationToken).ConfigureAwait(false);
        return charges.Select(BillingMaps.Charge).ToArray();
    }
}

public sealed record StartMyPayment(Guid ChargeId, string Provider);

public sealed class StartMyPaymentValidator : AbstractValidator<StartMyPayment>
{
    public StartMyPaymentValidator()
    {
        RuleFor(command => command.ChargeId).NotEmpty().WithErrorCode("charge.not_found");
        RuleFor(command => command.Provider).NotEmpty().WithErrorCode("payment.provider_invalid");
    }
}

public sealed class StartMyPaymentHandler(
    ICallerContext caller,
    IRightsRepository rights,
    IBillingRepository billing,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<StartMyPayment, PaymentView>
{
    public async Task<PaymentView> Handle(StartMyPayment command, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("charge.not_found");
        var charge = await billing.FindChargeForHolderAsync(command.ChargeId, holder.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("charge.not_found");
        var account = await billing.FindProviderAsync(command.Provider, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.Enabled)
            throw new NotFoundException("payment.provider_invalid");
        var payment = Payment.BeginCheckout(charge, command.Provider, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        billing.AddPayment(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Payment(payment);
    }
}

public sealed record GetMyPayment(Guid PaymentId);

public sealed class GetMyPaymentHandler(ICallerContext caller, IRightsRepository rights, IBillingRepository billing) : IQueryHandler<GetMyPayment, PaymentView>
{
    public async Task<PaymentView> Handle(GetMyPayment query, CancellationToken cancellationToken)
    {
        var holder = await rights.FindHolderByUserAsync(caller.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
        var payment = await billing.FindPaymentAsync(query.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
        var charge = await billing.FindChargeForHolderAsync(payment.ChargeId, holder.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
        _ = charge;
        return BillingMaps.Payment(payment);
    }
}
