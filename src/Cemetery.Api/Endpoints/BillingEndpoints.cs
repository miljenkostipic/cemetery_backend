using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cemeteries/{cemeteryId}/fee-schedules", GetFees).RequireAuthorization("tenant");
        api.MapPost("/cemeteries/{cemeteryId}/fee-schedules", PostFee).RequireAuthorization("tenant");
        api.MapDelete("/fee-schedules/{feeScheduleId}", DeleteFee).RequireAuthorization("tenant");
        api.MapGet("/cemeteries/{cemeteryId}/charges", GetCharges).RequireAuthorization("tenant");
        api.MapGet("/cemeteries/{cemeteryId}/arrears", GetArrears).RequireAuthorization("tenant");
        api.MapPost("/cemeteries/{cemeteryId}/charges/yearly-runs", PostYear).RequireAuthorization("tenant");
        api.MapPost("/cemeteries/{cemeteryId}/charges/reminders", PostReminders).RequireAuthorization("tenant");
        api.MapPost("/charges/{chargeId}/payments", PostPayment).RequireAuthorization("tenant");
        api.MapGet("/payments/{paymentId}/receipt", ReadReceipt).RequireAuthorization("tenant");
        api.MapPost("/payments/{paymentId}/voids", PostVoid).RequireAuthorization("tenant");
        api.MapGet("/payment-providers", GetProviders).RequireAuthorization("tenant");
        api.MapPut("/payment-providers/{provider}", PutProvider).RequireAuthorization("tenant");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<FeeScheduleView>>> GetFees(
        Guid cemeteryId,
        IQueryHandler<ListFeeSchedules, IReadOnlyList<FeeScheduleView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListFeeSchedules(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<FeeScheduleView>> PostFee(
        Guid cemeteryId,
        FeeScheduleBody body,
        ICommandHandler<AddFeeSchedule, FeeScheduleView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddFeeSchedule(cemeteryId, body.Kind, body.Name, body.Amount, body.Currency), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteFee(
        Guid feeScheduleId,
        ICommandHandler<RemoveFeeSchedule, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveFeeSchedule(feeScheduleId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<ChargeView>>> GetCharges(
        Guid cemeteryId,
        IQueryHandler<ListCharges, IReadOnlyList<ChargeView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListCharges(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<ArrearsRow>>> GetArrears(
        Guid cemeteryId,
        IQueryHandler<ListArrears, IReadOnlyList<ArrearsRow>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListArrears(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<YearlyRunView>> PostYear(
        Guid cemeteryId,
        YearlyRunBody body,
        ICommandHandler<RunYear, YearlyRunView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RunYear(cemeteryId, body.Year), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<ReminderRunView>> PostReminders(
        Guid cemeteryId,
        ICommandHandler<SendReminders, ReminderRunView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SendReminders(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PaymentView>> PostPayment(
        Guid chargeId,
        ManualPaymentBody body,
        ICommandHandler<RecordManualPayment, PaymentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RecordManualPayment(chargeId, body.Method, body.Amount, body.PaidOn, body.Reference), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<ReceiptView>> ReadReceipt(
        Guid paymentId,
        IQueryHandler<GetReceipt, ReceiptView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetReceipt(paymentId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<ChargeView>> PostVoid(
        Guid paymentId,
        BillingReasonBody body,
        ICommandHandler<VoidPayment, ChargeView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new VoidPayment(paymentId, body.Reason), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<PaymentProviderView>>> GetProviders(
        IQueryHandler<ListPaymentProviders, IReadOnlyList<PaymentProviderView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListPaymentProviders(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PaymentProviderView>> PutProvider(
        string provider,
        PaymentProviderBody body,
        ICommandHandler<SavePaymentProvider, PaymentProviderView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SavePaymentProvider(provider, body.MerchantId, body.Secret, body.Enabled), cancellationToken).ConfigureAwait(false));
}
