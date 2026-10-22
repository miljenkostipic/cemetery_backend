using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Billing;
using FluentValidation;

namespace Cemetery.Application.Billing;

public sealed record RecordManualPayment(Guid ChargeId, string Method, string Amount, DateOnly PaidOn, string? Reference);

public sealed class RecordManualPaymentValidator : AbstractValidator<RecordManualPayment>
{
    public RecordManualPaymentValidator()
    {
        RuleFor(command => command.ChargeId).NotEmpty().WithErrorCode("charge.not_found");
        RuleFor(command => command.Method).Must(ManualMethods.IsKnown).WithErrorCode("payment.method_invalid");
        RuleFor(command => command.Amount).Must(FeeAmount.Accepts).WithErrorCode(PaymentAmount.Invalid);
        RuleFor(command => command.PaidOn).Must(PaymentDate.Accepts).WithErrorCode(PaymentDate.Invalid);
        RuleFor(command => command.Reference).Must(PaymentReference.Accepts).WithErrorCode(PaymentReference.Invalid);
    }
}

public sealed class RecordManualPaymentHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<RecordManualPayment, PaymentView>
{
    public async Task<PaymentView> Handle(RecordManualPayment command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var charge = await billing.FindChargeAsync(command.ChargeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("charge.not_found");
        var payment = Payment.Record(charge, command.Method, command.Amount, command.PaidOn, command.Reference);
        billing.AddPayment(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Payment(payment);
    }
}

public sealed record GetReceipt(Guid PaymentId);

public sealed class GetReceiptHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing) : IQueryHandler<GetReceipt, ReceiptView>
{
    public async Task<ReceiptView> Handle(GetReceipt query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var payment = await billing.FindPaymentAsync(query.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
        return BillingMaps.Receipt(payment);
    }
}

public sealed record VoidPayment(Guid PaymentId, string Reason);

public sealed class VoidPaymentValidator : AbstractValidator<VoidPayment>
{
    public VoidPaymentValidator()
    {
        RuleFor(command => command.PaymentId).NotEmpty().WithErrorCode("payment.not_found");
        RuleFor(command => command.Reason).Must(BillingReason.Accepts).WithErrorCode("payment.reason_invalid");
    }
}

public sealed class VoidPaymentHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<VoidPayment, ChargeView>
{
    public async Task<ChargeView> Handle(VoidPayment command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var payment = await FindAsync(command.PaymentId, cancellationToken).ConfigureAwait(false);
        var charge = await billing.FindChargeAsync(payment.ChargeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("charge.not_found");
        payment.Void(command.Reason);
        charge.Reopen();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Charge(charge);
    }

    private async Task<Payment> FindAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await billing.FindPaymentAsync(paymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
}
