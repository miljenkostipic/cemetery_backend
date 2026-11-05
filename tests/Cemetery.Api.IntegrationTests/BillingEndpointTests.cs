using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class BillingEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var fee = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", Fee("annual", "Upkeep"), CancellationToken);
        var extra = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", Fee("service", "Opening"), CancellationToken);
        Assert.Contains(fee, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", CancellationToken)));
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/fee-schedules/{extra}", CancellationToken));

        var run = await OfficeWorld.Post(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/charges/yearly-runs", new { year = 2026 }, CancellationToken);
        Assert.True(run.GetProperty("chargeCount").GetInt32() > 0);
        await OfficeWorld.Post(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/charges/reminders", new { }, CancellationToken);
        var charges = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/charges", CancellationToken);
        var charge = ApiAssert.Rows(charges).EnumerateArray().First();
        var chargeId = charge.GetProperty("id").GetGuid();
        var arrears = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/arrears", CancellationToken);
        Assert.Contains(chargeId, ApiAssert.Rows(arrears).EnumerateArray().Select(item => item.GetProperty("chargeId").GetGuid()));

        var payment = await OfficeWorld.Post(world.Clerk, $"/api/v1/charges/{chargeId}/payments", new
        {
            method = "bank",
            amount = charge.GetProperty("amount").GetString(),
            paidOn = "2026-04-01",
            reference = "REF-1",
        }, CancellationToken);
        var paymentId = ApiAssert.Id(payment);
        var receipt = await OfficeWorld.Get(world.Clerk, $"/api/v1/payments/{paymentId}/receipt", CancellationToken);
        Assert.Equal(paymentId, receipt.GetProperty("paymentId").GetGuid());
        var voided = await OfficeWorld.Post(world.Clerk, $"/api/v1/payments/{paymentId}/voids", new { reason = "Paid in error" }, CancellationToken);
        Assert.Equal(chargeId, ApiAssert.Id(voided));

        await OfficeWorld.Get(world.Clerk, "/api/v1/payment-providers", CancellationToken);
        var saved = await world.Clerk.PutAsJsonAsync("/api/v1/payment-providers/monri", new { merchantId = "merchant-9", secret = "secret-ok", enabled = true }, CancellationToken);
        await ApiAssert.Ok(saved);
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", Fee("annual", "Upkeep"), CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/fee-schedules/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/charges", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/arrears", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/charges/yearly-runs", new { year = 2026 }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/cemeteries/{world.CemeteryId}/charges/reminders", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/charges/{Guid.NewGuid()}/payments", new { method = "cash", amount = "1.00", paidOn = "2026-04-01" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/payments/{Guid.NewGuid()}/receipt", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/payments/{Guid.NewGuid()}/voids", new { reason = "Nope" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/payment-providers", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync("/api/v1/payment-providers/monri", new { merchantId = "merchant-9", secret = "secret-ok", enabled = true }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var fee = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", Fee("annual", "Upkeep"), CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", fee, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/charges", null, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/arrears", null, CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.DeleteAsync($"/api/v1/fee-schedules/{fee}", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/fee-schedules", new { kind = "nope", name = "Upkeep", amount = "25.00", currency = "EUR" }, CancellationToken), HttpStatusCode.BadRequest, "fee.kind_invalid");
        await ApiAssert.Problem(await world.Clerk.DeleteAsync($"/api/v1/fee-schedules/{Guid.Empty}", CancellationToken), HttpStatusCode.BadRequest, "fee.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsync("/api/v1/cemeteries/00000000-0000-0000-0000-000000000000/charges/reminders", null, CancellationToken), HttpStatusCode.BadRequest, "cemetery.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/charges/{Guid.Empty}/payments", new { method = "bank", amount = "25.00", paidOn = "2026-04-01" }, CancellationToken), HttpStatusCode.BadRequest, "charge.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/payments/{Guid.Empty}/voids", new { reason = "Nope" }, CancellationToken), HttpStatusCode.BadRequest, "payment.not_found");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync("/api/v1/payment-providers/nope", new { merchantId = "merchant-9", secret = "secret-ok", enabled = true }, CancellationToken), HttpStatusCode.BadRequest, "payment.provider_invalid");
    }

    private static object Fee(string kind, string name) => new { kind, name, amount = "25.00", currency = "EUR" };
}
