using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace Cemetery.Api.IntegrationTests;

public sealed class PhotoEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var first = await Upload(world.Clerk, world.SiteId, Png(2400, 1200), "image/png", "Prije obnove", "2026-05-01");
        Assert.True(first.GetProperty("isCover").GetBoolean());
        Assert.False(first.GetProperty("showOnMemorial").GetBoolean());
        Assert.Equal(1600, first.GetProperty("width").GetInt32());
        Assert.Equal(800, first.GetProperty("height").GetInt32());
        Assert.Equal("Prije obnove", first.GetProperty("caption").GetString());
        Assert.Equal("2026-05-01", first.GetProperty("takenOn").GetString());
        var firstId = ApiAssert.Id(first);
        Assert.Equal($"/api/v1/grave-site-photos/{firstId}/image", first.GetProperty("imageUrl").GetString());
        Assert.Equal($"/api/v1/grave-site-photos/{firstId}/image?size=thumb", first.GetProperty("thumbnailUrl").GetString());

        var second = await Upload(world.Field, world.SiteId, Png(200, 100), "image/png", null, null);
        var secondId = ApiAssert.Id(second);
        Assert.False(second.GetProperty("isCover").GetBoolean());
        Assert.Equal([firstId, secondId], ApiAssert.Ids(await OfficeWorld.Get(world.Field, $"/api/v1/grave-sites/{world.SiteId}/photos", CancellationToken)));

        await AssertImage(world.Field, first.GetProperty("imageUrl").GetString()!, 1600, 800);
        await AssertImage(world.Clerk, first.GetProperty("thumbnailUrl").GetString()!, 320, 160);
        await AssertImage(world.Clerk, $"/api/v1/grave-site-photos/{firstId}/image?size=full", 1600, 800);

        var updated = await ApiAssert.Ok(await world.Clerk.PutAsJsonAsync($"/api/v1/grave-site-photos/{secondId}", new { caption = "Nakon obnove", takenOn = "2026-06-01", showOnMemorial = true }, CancellationToken));
        Assert.True(updated.GetProperty("showOnMemorial").GetBoolean());
        Assert.Equal("Nakon obnove", updated.GetProperty("caption").GetString());

        var dossier = await OfficeWorld.Get(world.Field, $"/api/v1/grave-sites/{world.SiteId}/dossier", CancellationToken);
        Assert.Equal(first.GetProperty("imageUrl").GetString(), dossier.GetProperty("primaryPhotoUrl").GetString());

        var cover = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-site-photos/{secondId}/cover", new { }, CancellationToken);
        Assert.True(cover.GetProperty("isCover").GetBoolean());
        var listed = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/photos", CancellationToken);
        Assert.Equal([secondId, firstId], ApiAssert.Ids(listed));
        Assert.Equal([true, false], listed.EnumerateArray().Select(photo => photo.GetProperty("isCover").GetBoolean()));
        dossier = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/dossier", CancellationToken);
        Assert.Equal(second.GetProperty("imageUrl").GetString(), dossier.GetProperty("primaryPhotoUrl").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await world.Clerk.DeleteAsync($"/api/v1/grave-site-photos/{secondId}", CancellationToken)).StatusCode);
        listed = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/photos", CancellationToken);
        Assert.Equal([firstId], ApiAssert.Ids(listed));
        Assert.False(listed[0].GetProperty("isCover").GetBoolean());
        await ApiAssert.Problem(await world.Clerk.GetAsync($"/api/v1/grave-site-photos/{secondId}/image", CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        dossier = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/dossier", CancellationToken);
        Assert.Equal(JsonValueKind.Null, dossier.GetProperty("primaryPhotoUrl").ValueKind);
    }

    [Fact]
    public async Task A_field_worker_uploads_jpeg_and_webp_from_the_phone()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);

        var jpeg = await Upload(world.Field, world.SecondSiteId, Encode(400, 300, SKEncodedImageFormat.Jpeg), "image/jpeg", null, null);
        var webp = await Upload(world.Field, world.SecondSiteId, Encode(400, 300, SKEncodedImageFormat.Webp), "image/webp", "Sa terena", null);

        Assert.True(jpeg.GetProperty("isCover").GetBoolean());
        Assert.False(webp.GetProperty("isCover").GetBoolean());
        await AssertImage(world.Field, webp.GetProperty("imageUrl").GetString()!, 400, 300);
    }

    [Fact]
    public async Task Refusals()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var photo = await Upload(world.Clerk, world.SiteId, Png(50, 50), "image/png", null, null);
        var photoId = ApiAssert.Id(photo);
        var url = $"/api/v1/grave-sites/{world.SiteId}/photos";

        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, Body("image/gif", "R0lGODlh"), CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.type_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, Body("image/jpeg", Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 1, 2])), CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.type_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, Body("image/jpeg", ""), CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.empty");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, Body("image/jpeg", new string('A', 21_000_000)), CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.too_large");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, new { contentType = "image/png", contentBase64 = Png(10, 10), caption = new string('x', 301) }, CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.caption_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, new { contentType = "image/png", contentBase64 = Png(10, 10), takenOn = "2999-01-01" }, CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.date_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/grave-sites/{Guid.NewGuid()}/photos", Body("image/png", Png(10, 10)), CancellationToken), HttpStatusCode.NotFound, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/grave-sites/{Guid.Empty}/photos", Body("image/png", Png(10, 10)), CancellationToken), HttpStatusCode.BadRequest, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.GetAsync($"/api/v1/grave-sites/{Guid.NewGuid()}/photos", CancellationToken), HttpStatusCode.NotFound, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.GetAsync($"/api/v1/grave-site-photos/{photoId}/image?size=huge", CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.size_invalid");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync($"/api/v1/grave-site-photos/{photoId}", new { takenOn = "2999-01-01", showOnMemorial = false }, CancellationToken), HttpStatusCode.UnprocessableEntity, "photo.date_invalid");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync($"/api/v1/grave-site-photos/{Guid.NewGuid()}", new { showOnMemorial = false }, CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/grave-site-photos/{Guid.NewGuid()}/cover", new { }, CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        await ApiAssert.Problem(await world.Clerk.DeleteAsync($"/api/v1/grave-site-photos/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
    }

    [Fact]
    public async Task A_field_worker_cannot_edit_and_a_holder_cannot_touch_photos()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var photoId = ApiAssert.Id(await Upload(world.Clerk, world.SiteId, Png(50, 50), "image/png", null, null));

        await ApiAssert.Problem(await world.Field.PutAsJsonAsync($"/api/v1/grave-site-photos/{photoId}", new { showOnMemorial = true }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Field.PostAsJsonAsync($"/api/v1/grave-site-photos/{photoId}/cover", new { }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Field.DeleteAsync($"/api/v1/grave-site-photos/{photoId}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/photos", Body("image/png", Png(10, 10)), CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/grave-sites/{world.SiteId}/photos", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/grave-site-photos/{photoId}/image", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Another_tenant_never_sees_the_photos()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var photoId = ApiAssert.Id(await Upload(world.Clerk, world.SiteId, Png(50, 50), "image/png", null, null));

        await OfficeWorld.Hidden(world.Other, $"/api/v1/grave-sites/{world.SiteId}/photos", photoId, CancellationToken);
        await ApiAssert.Problem(await world.Other.PostAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/photos", Body("image/png", Png(10, 10)), CancellationToken), HttpStatusCode.NotFound, "grave_site.not_found");
        await ApiAssert.Problem(await world.Other.GetAsync($"/api/v1/grave-site-photos/{photoId}/image", CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        await ApiAssert.Problem(await world.Other.PutAsJsonAsync($"/api/v1/grave-site-photos/{photoId}", new { showOnMemorial = true }, CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        await ApiAssert.Problem(await world.Other.PostAsJsonAsync($"/api/v1/grave-site-photos/{photoId}/cover", new { }, CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        await ApiAssert.Problem(await world.Other.DeleteAsync($"/api/v1/grave-site-photos/{photoId}", CancellationToken), HttpStatusCode.NotFound, "photo.not_found");
        Assert.Equal([photoId], ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/photos", CancellationToken)));
    }

    private static async Task<JsonElement> Upload(HttpClient client, Guid siteId, string contentBase64, string contentType, string? caption, string? takenOn) =>
        await OfficeWorld.Post(client, $"/api/v1/grave-sites/{siteId}/photos", new { contentType, contentBase64, caption, takenOn }, CancellationToken);

    private static async Task AssertImage(HttpClient client, string url, int width, int height)
    {
        var response = await client.GetAsync(url, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("private, max-age=31536000, immutable", response.Headers.NonValidated["Cache-Control"].ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken);
        using var image = SKBitmap.Decode(bytes);
        Assert.Equal((width, height), (image.Width, image.Height));
    }

    private static object Body(string contentType, string contentBase64) => new { contentType, contentBase64 };

    private static string Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    private static string Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.SlateGray);
        using var data = bitmap.Encode(format, 90);
        return Convert.ToBase64String(data.ToArray());
    }
}
