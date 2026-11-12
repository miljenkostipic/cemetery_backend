using Cemetery.Domain.Identity;
using Cemetery.Domain.Photos;

namespace Cemetery.Domain.Tests;

public sealed class PhotoRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Site = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0];
    private static readonly byte[] Webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8];

    [Fact]
    public void Jpeg_png_and_webp_uploads_decode_to_their_bytes()
    {
        Assert.Equal(Jpeg, GraveSitePhotoBytes.Decode("image/jpeg", Convert.ToBase64String(Jpeg)));
        Assert.Equal(Png, GraveSitePhotoBytes.Decode("image/png", Convert.ToBase64String(Png)));
        Assert.Equal(Webp, GraveSitePhotoBytes.Decode("image/webp", " " + Convert.ToBase64String(Webp) + " "));
    }

    [Theory]
    [InlineData("image/gif", "R0lGODlh", "photo.type_invalid")]
    [InlineData(null, "R0lGODlh", "photo.type_invalid")]
    [InlineData("image/jpeg", null, "photo.empty")]
    [InlineData("image/jpeg", "   ", "photo.empty")]
    [InlineData("image/jpeg", "not base64!", "photo.type_invalid")]
    [InlineData("image/png", "/9j/4AAQ", "photo.type_invalid")]
    [InlineData("image/jpeg", "iVBORw0KGgo=", "photo.type_invalid")]
    [InlineData("image/webp", "UklGRg==", "photo.type_invalid")]
    [InlineData("image/webp", "UklGRgAAAABBQkNE", "photo.type_invalid")]
    public void Uploads_that_are_not_the_declared_image_are_refused(string? type, string? encoded, string code)
    {
        var error = Assert.Throws<DomainRuleException>(() => GraveSitePhotoBytes.Decode(type, encoded));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void Uploads_over_fifteen_megabytes_are_refused()
    {
        var justOver = new byte[GraveSitePhotoBytes.MaxBytes + 1];
        Jpeg.CopyTo(justOver, 0);
        var huge = new string('A', (GraveSitePhotoBytes.MaxBytes / 3 * 4) + 1000);

        Assert.Equal("photo.too_large", Assert.Throws<DomainRuleException>(() => GraveSitePhotoBytes.Decode("image/jpeg", Convert.ToBase64String(justOver))).Code);
        Assert.Equal("photo.too_large", Assert.Throws<DomainRuleException>(() => GraveSitePhotoBytes.Decode("image/jpeg", huge)).Code);
    }

    [Fact]
    public void Details_trim_the_caption_and_accept_today()
    {
        var details = GraveSitePhotoDetails.Create("  Spomenik nakon obnove  ", Today, Today);
        var empty = GraveSitePhotoDetails.Create("   ", null, Today);

        Assert.Equal("Spomenik nakon obnove", details.Caption);
        Assert.Equal(Today, details.TakenOn);
        Assert.Null(empty.Caption);
        Assert.Null(empty.TakenOn);
        Assert.Equal(300, GraveSitePhotoDetails.Create(new string('a', 300), null, Today).Caption!.Length);
    }

    [Fact]
    public void Details_refuse_a_long_or_control_caption_and_a_future_day()
    {
        Assert.Equal("photo.caption_invalid", Assert.Throws<DomainRuleException>(() => GraveSitePhotoDetails.Create(new string('a', 301), null, Today)).Code);
        Assert.Equal("photo.caption_invalid", Assert.Throws<DomainRuleException>(() => GraveSitePhotoDetails.Create("a\u0000b", null, Today)).Code);
        Assert.Equal("photo.date_invalid", Assert.Throws<DomainRuleException>(() => GraveSitePhotoDetails.Create(null, Today.AddDays(1), Today)).Code);
    }

    [Fact]
    public void A_photo_keeps_its_files_details_and_starts_private_and_not_cover()
    {
        var photo = Take();

        Assert.NotEqual(Guid.Empty, photo.Id);
        Assert.Equal(Tenant, photo.TenantId);
        Assert.Equal(Site, photo.GraveSiteId);
        Assert.Equal("full", photo.FullFileId);
        Assert.Equal("thumb", photo.ThumbFileId);
        Assert.Equal(1600, photo.Width);
        Assert.Equal(1200, photo.Height);
        Assert.Equal("Grob", photo.Caption);
        Assert.Equal(Today, photo.TakenOn);
        Assert.Equal(Now, photo.CreatedAt);
        Assert.False(photo.IsCover);
        Assert.False(photo.ShowOnMemorial);
    }

    [Fact]
    public void A_photo_needs_a_tenant_a_site_and_real_files()
    {
        var files = new GraveSitePhotoFiles("full", "thumb", 10, 10);
        var details = GraveSitePhotoDetails.Create(null, null, Today);

        Assert.Equal("grave_site.not_found", Assert.Throws<DomainRuleException>(() => GraveSitePhoto.Take(Guid.Empty, Site, files, details, Now)).Code);
        Assert.Equal("grave_site.not_found", Assert.Throws<DomainRuleException>(() => GraveSitePhoto.Take(Tenant, Guid.Empty, files, details, Now)).Code);
        Assert.Equal("photo.type_invalid", Assert.Throws<DomainRuleException>(() => new GraveSitePhotoFiles("", "thumb", 10, 10)).Code);
        Assert.Equal("photo.type_invalid", Assert.Throws<DomainRuleException>(() => new GraveSitePhotoFiles("full", " ", 10, 10)).Code);
        Assert.Equal("photo.type_invalid", Assert.Throws<DomainRuleException>(() => new GraveSitePhotoFiles("full", "thumb", 0, 10)).Code);
        Assert.Equal("photo.type_invalid", Assert.Throws<DomainRuleException>(() => new GraveSitePhotoFiles("full", "thumb", 10, 0)).Code);
    }

    [Fact]
    public void A_clerk_describes_a_photo_and_shows_or_hides_it_on_the_memorial()
    {
        var photo = Take();

        photo.Describe(GraveSitePhotoDetails.Create("Novi natpis", null, Today));
        photo.ShowOnMemorialPage();
        Assert.Equal("Novi natpis", photo.Caption);
        Assert.Null(photo.TakenOn);
        Assert.True(photo.ShowOnMemorial);

        photo.HideFromMemorialPage();
        Assert.False(photo.ShowOnMemorial);
    }

    [Fact]
    public void Choosing_a_cover_leaves_exactly_one_cover()
    {
        var first = Take();
        var second = Take();
        var third = Take();
        first.MarkCover();
        second.MarkCover();

        GraveSitePhoto.ChooseCover([first, second], third);

        Assert.False(first.IsCover);
        Assert.False(second.IsCover);
        Assert.True(third.IsCover);
        third.ClearCover();
        Assert.False(third.IsCover);
    }

    [Fact]
    public void A_cover_is_chosen_only_among_photos_of_the_same_site()
    {
        var elsewhere = GraveSitePhoto.Take(Tenant, Guid.CreateVersion7(), new GraveSitePhotoFiles("a", "b", 1, 1), GraveSitePhotoDetails.Create(null, null, Today), Now);

        var error = Assert.Throws<DomainRuleException>(() => GraveSitePhoto.ChooseCover([elsewhere], Take()));

        Assert.Equal("photo.not_found", error.Code);
    }

    private static GraveSitePhoto Take() =>
        GraveSitePhoto.Take(Tenant, Site, new GraveSitePhotoFiles("full", "thumb", 1600, 1200), GraveSitePhotoDetails.Create("Grob", Today, Today), Now);
}
