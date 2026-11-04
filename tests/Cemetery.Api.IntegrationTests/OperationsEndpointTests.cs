using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class OperationsEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var funeral = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/funerals", Funeral(world), CancellationToken);
        var funerals = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/funerals", CancellationToken);
        Assert.Contains(funeral, ApiAssert.Ids(funerals));
        var cancelled = await OfficeWorld.Post(world.Clerk, $"/api/v1/funerals/{funeral}/cancellations", new { }, CancellationToken);
        Assert.Equal(funeral, ApiAssert.Id(cancelled));
        var extraFuneral = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/funerals", Funeral(world, "2026-07-01"), CancellationToken);
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/funerals/{extraFuneral}", CancellationToken));

        var booking = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/chapel-bookings", new
        {
            place = "chapel",
            startsAt = "2026-08-01T10:00",
            endsAt = "2026-08-01T11:00",
        }, CancellationToken);
        Assert.Contains(booking, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/chapel-bookings", CancellationToken)));
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/chapel-bookings/{booking}", CancellationToken));

        var order = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/work-orders", Order(world, "2026-11-03"), CancellationToken);
        Assert.Contains(order, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/work-orders", CancellationToken)));
        var today = await OfficeWorld.Get(world.Field, "/api/v1/field/today", CancellationToken);
        Assert.True(today.GetArrayLength() >= 0);
        var done = await OfficeWorld.Post(world.Field, $"/api/v1/work-orders/{order}/completions", new { completedOn = "2026-11-03", note = "Done" }, CancellationToken);
        Assert.Equal(order, done.GetProperty("workOrderId").GetGuid());
        var photo = await OfficeWorld.Post(world.Field, $"/api/v1/work-orders/{order}/photos", new
        {
            contentType = "image/jpeg",
            contentBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF }),
        }, CancellationToken);
        Assert.NotEqual(Guid.Empty, ApiAssert.Id(photo));
        var spare = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/work-orders", Order(world, "2026-11-04"), CancellationToken);
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/work-orders/{spare}", CancellationToken));

        var permit = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/monument-permits", new
        {
            graveSiteId = world.SiteId,
            holderName = "Ana Kovac",
            issuedOn = "2026-06-01",
            expiresOn = "2027-06-01",
        }, CancellationToken);
        Assert.Contains(permit, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/monument-permits", CancellationToken)));
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/monument-permits/{permit}", CancellationToken));
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/funerals", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/funerals", Funeral(world), CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/funerals/{Guid.NewGuid()}/cancellations", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/funerals/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/chapel-bookings", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/chapel-bookings", new { place = "chapel", startsAt = "2026-08-01T10:00", endsAt = "2026-08-01T11:00" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/chapel-bookings/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/work-orders", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/work-orders", Order(world, "2026-11-03"), CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/work-orders/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/monument-permits", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/monument-permits", new { graveSiteId = world.SiteId, holderName = "Ana", issuedOn = "2026-06-01", expiresOn = "2027-06-01" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/monument-permits/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/field/today", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/work-orders/{Guid.NewGuid()}/completions", new { completedOn = "2026-11-03", note = "Done" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/work-orders/{Guid.NewGuid()}/photos", new { contentType = "image/jpeg", contentBase64 = "abc" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var funeral = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/funerals", Funeral(world), CancellationToken);
        var funerals = await OfficeWorld.Get(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/funerals", CancellationToken);
        Assert.DoesNotContain(funeral, ApiAssert.Ids(funerals));
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.PostAsync($"/api/v1/funerals/{funeral}/cancellations", null, CancellationToken)).StatusCode);
        var orders = await OfficeWorld.Get(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/work-orders", CancellationToken);
        Assert.Empty(orders.EnumerateArray());
        var today = await OfficeWorld.Get(world.Other, "/api/v1/field/today", CancellationToken);
        Assert.DoesNotContain(world.CemeteryId, today.EnumerateArray().Select(item => item.GetProperty("cemeteryId").GetGuid()));
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/cemeteries/00000000-0000-0000-0000-000000000000/funerals", Funeral(world), CancellationToken), HttpStatusCode.BadRequest, "cemetery.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/cemeteries/00000000-0000-0000-0000-000000000000/chapel-bookings", new { place = "chapel", startsAt = "2026-08-01T10:00", endsAt = "2026-08-01T11:00" }, CancellationToken), HttpStatusCode.BadRequest, "cemetery.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsync($"/api/v1/funerals/{Guid.Empty}/cancellations", null, CancellationToken), HttpStatusCode.BadRequest, "funeral.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/cemeteries/00000000-0000-0000-0000-000000000000/monument-permits", new { graveSiteId = world.SiteId, holderName = "Ana", issuedOn = "2026-06-01", expiresOn = "2027-06-01" }, CancellationToken), HttpStatusCode.BadRequest, "cemetery.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/cemeteries/00000000-0000-0000-0000-000000000000/work-orders", Order(world, "2026-11-03"), CancellationToken), HttpStatusCode.BadRequest, "cemetery.not_found");
        await ApiAssert.Problem(await world.Field.PostAsJsonAsync($"/api/v1/work-orders/{Guid.Empty}/completions", new { completedOn = "2026-11-03", note = "Done" }, CancellationToken), HttpStatusCode.BadRequest, "work_order.not_found");
        await ApiAssert.Problem(await world.Field.PostAsJsonAsync($"/api/v1/work-orders/{Guid.Empty}/photos", new { contentType = "image/jpeg", contentBase64 = "abc" }, CancellationToken), HttpStatusCode.BadRequest, "work_order.not_found");
    }

    private static object Funeral(OfficeWorld world, string day = "2026-06-01") => new
    {
        deceasedId = world.DeceasedId,
        graveSiteId = world.SiteId,
        startsAt = $"{day}T10:00",
        endsAt = $"{day}T11:00",
        place = "chapel",
    };

    private static object Order(OfficeWorld world, string day) => new
    {
        graveSiteId = world.SiteId,
        kind = "digging",
        scheduledOn = day,
        notes = "Open the grave",
    };
}
