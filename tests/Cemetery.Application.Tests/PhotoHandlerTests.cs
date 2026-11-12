using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Photos;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Photos;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class PhotoHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Jpeg = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]);

    [Fact]
    public async Task The_first_photo_of_a_grave_becomes_its_cover()
    {
        var world = new World();

        var view = await world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/jpeg", Jpeg, " Grob ", new DateOnly(2026, 11, 1)), CancellationToken.None);

        Assert.True(view.IsCover);
        Assert.False(view.ShowOnMemorial);
        Assert.Equal("Grob", view.Caption);
        Assert.Equal(new DateOnly(2026, 11, 1), view.TakenOn);
        Assert.Equal((1600, 1200), (view.Width, view.Height));
        Assert.Equal(Now, view.CreatedAt);
        Assert.Equal(world.Site.Id, view.GraveSiteId);
        Assert.Equal($"/api/v1/grave-site-photos/{view.Id}/image", view.ImageUrl);
        Assert.Equal($"/api/v1/grave-site-photos/{view.Id}/image?size=thumb", view.ThumbnailUrl);
        await world.Storage.Received(1).PutAsync(world.TenantId, world.Processed.Full, Arg.Any<CancellationToken>());
        await world.Storage.Received(1).PutAsync(world.TenantId, world.Processed.Thumbnail, Arg.Any<CancellationToken>());
        await world.Photos.Received(1).AddAsync(Arg.Is<GraveSitePhoto>(photo => photo.FullFileId == "full-key" && photo.ThumbFileId == "thumb-key"), Arg.Any<CancellationToken>());
        await world.Unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_later_photo_is_not_the_cover()
    {
        var world = new World();
        world.Has(world.Photo(cover: true));

        var view = await world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/jpeg", Jpeg, null, null), CancellationToken.None);

        Assert.False(view.IsCover);
        Assert.Null(view.Caption);
    }

    [Fact]
    public async Task An_upload_refuses_a_missing_site_bad_details_and_undecodable_bytes_before_storing()
    {
        var world = new World();
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => world.Add().Handle(new AddGraveSitePhoto(Guid.CreateVersion7(), "image/jpeg", Jpeg, null, null), CancellationToken.None));
        var future = await Assert.ThrowsAsync<DomainRuleException>(() => world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/jpeg", Jpeg, null, new DateOnly(2026, 11, 12)), CancellationToken.None));
        var caption = await Assert.ThrowsAsync<DomainRuleException>(() => world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/jpeg", Jpeg, new string('x', 301), null), CancellationToken.None));
        var type = await Assert.ThrowsAsync<DomainRuleException>(() => world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/gif", Jpeg, null, null), CancellationToken.None));
        world.Images.Process(Arg.Any<byte[]>()).Returns(_ => throw new DomainRuleException("photo.type_invalid"));
        var garbage = await Assert.ThrowsAsync<DomainRuleException>(() => world.Add().Handle(new AddGraveSitePhoto(world.Site.Id, "image/jpeg", Jpeg, null, null), CancellationToken.None));

        Assert.Equal("grave_site.not_found", missing.Code);
        Assert.Equal("photo.date_invalid", future.Code);
        Assert.Equal("photo.caption_invalid", caption.Code);
        Assert.Equal("photo.type_invalid", type.Code);
        Assert.Equal("photo.type_invalid", garbage.Code);
        await world.Storage.DidNotReceiveWithAnyArgs().PutAsync(default, default!, TestContext.Current.CancellationToken);
        await world.Unit.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Listing_returns_the_repository_order_for_an_existing_site()
    {
        var world = new World();
        var cover = world.Photo(cover: true);
        var other = world.Photo(cover: false);
        world.Has(cover, other);
        var handler = new ListGraveSitePhotosHandler(world.Caller, world.Layouts, world.Photos);

        var listed = await handler.Handle(new ListGraveSitePhotos(world.Site.Id), CancellationToken.None);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ListGraveSitePhotos(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal([cover.Id, other.Id], listed.Select(view => view.Id));
        Assert.Equal("grave_site.not_found", missing.Code);
    }

    [Theory]
    [InlineData(null, "full-key")]
    [InlineData("", "full-key")]
    [InlineData("full", "full-key")]
    [InlineData("thumb", "thumb-key")]
    public async Task The_image_is_the_full_or_thumbnail_jpeg(string? size, string key)
    {
        var world = new World();
        var photo = world.Photo(cover: true);
        world.Has(photo);
        world.Storage.OpenAsync(key, Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

        var file = await new GetGraveSitePhotoImageHandler(world.Caller, world.Photos, world.Storage).Handle(new GetGraveSitePhotoImage(photo.Id, size), CancellationToken.None);

        Assert.Equal([1, 2, 3], file.Content);
        Assert.Equal("image/jpeg", file.ContentType);
    }

    [Fact]
    public async Task The_image_refuses_an_unknown_size_photo_or_file()
    {
        var world = new World();
        var photo = world.Photo(cover: true);
        world.Has(photo);
        world.Storage.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        var handler = new GetGraveSitePhotoImageHandler(world.Caller, world.Photos, world.Storage);

        var size = await Assert.ThrowsAsync<DomainRuleException>(() => handler.Handle(new GetGraveSitePhotoImage(photo.Id, "huge"), CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetGraveSitePhotoImage(Guid.CreateVersion7(), null), CancellationToken.None));
        var lost = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetGraveSitePhotoImage(photo.Id, null), CancellationToken.None));

        Assert.Equal("photo.size_invalid", size.Code);
        Assert.Equal("photo.not_found", unknown.Code);
        Assert.Equal("photo.not_found", lost.Code);
    }

    [Fact]
    public async Task An_update_rewrites_the_details_and_the_memorial_flag()
    {
        var world = new World();
        var photo = world.Photo(cover: false);
        world.Has(photo);
        var handler = new UpdateGraveSitePhotoHandler(world.Caller, world.Photos, world.Unit, world.Clock);

        var shown = await handler.Handle(new UpdateGraveSitePhoto(photo.Id, "Nakon obnove", new DateOnly(2026, 5, 1), true), CancellationToken.None);
        Assert.True(shown.ShowOnMemorial);
        Assert.Equal("Nakon obnove", shown.Caption);
        Assert.Equal(new DateOnly(2026, 5, 1), shown.TakenOn);

        var hidden = await handler.Handle(new UpdateGraveSitePhoto(photo.Id, null, null, false), CancellationToken.None);
        Assert.False(hidden.ShowOnMemorial);
        Assert.Null(hidden.Caption);
        Assert.Null(hidden.TakenOn);
        await world.Unit.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());

        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdateGraveSitePhoto(Guid.CreateVersion7(), null, null, false), CancellationToken.None));
        var future = await Assert.ThrowsAsync<DomainRuleException>(() => handler.Handle(new UpdateGraveSitePhoto(photo.Id, null, new DateOnly(2027, 1, 1), false), CancellationToken.None));
        Assert.Equal("photo.not_found", missing.Code);
        Assert.Equal("photo.date_invalid", future.Code);
    }

    [Fact]
    public async Task Setting_a_cover_clears_the_old_one()
    {
        var world = new World();
        var old = world.Photo(cover: true);
        var chosen = world.Photo(cover: false);
        world.Has(old, chosen);
        var handler = new SetGraveSitePhotoCoverHandler(world.Caller, world.Photos, world.Unit);

        var view = await handler.Handle(new SetGraveSitePhotoCover(chosen.Id), CancellationToken.None);
        var again = await handler.Handle(new SetGraveSitePhotoCover(chosen.Id), CancellationToken.None);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new SetGraveSitePhotoCover(Guid.CreateVersion7()), CancellationToken.None));

        Assert.True(view.IsCover);
        Assert.True(again.IsCover);
        Assert.False(old.IsCover);
        Assert.True(chosen.IsCover);
        Assert.Equal("photo.not_found", missing.Code);
    }

    [Fact]
    public async Task Deleting_the_cover_removes_its_files_and_promotes_nothing()
    {
        var world = new World();
        var cover = world.Photo(cover: true);
        var other = world.Photo(cover: false);
        world.Has(cover, other);
        var handler = new DeleteGraveSitePhotoHandler(world.Caller, world.Photos, world.Storage, world.Unit);

        Assert.True(await handler.Handle(new DeleteGraveSitePhoto(cover.Id), CancellationToken.None));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteGraveSitePhoto(Guid.CreateVersion7()), CancellationToken.None));

        await world.Photos.Received(1).RemoveAsync(cover, Arg.Any<CancellationToken>());
        await world.Storage.Received(1).DeleteAsync("full-key", Arg.Any<CancellationToken>());
        await world.Storage.Received(1).DeleteAsync("thumb-key", Arg.Any<CancellationToken>());
        await world.Unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.False(other.IsCover);
        Assert.Equal("photo.not_found", missing.Code);
    }

    [Fact]
    public void Validators_refuse_empty_ids()
    {
        Assert.Contains(new AddGraveSitePhotoValidator().Validate(new AddGraveSitePhoto(Guid.Empty, "image/jpeg", Jpeg, null, null)).Errors, error => error.ErrorCode == "grave_site.not_found");
        Assert.Contains(new UpdateGraveSitePhotoValidator().Validate(new UpdateGraveSitePhoto(Guid.Empty, null, null, false)).Errors, error => error.ErrorCode == "photo.not_found");
        Assert.Contains(new SetGraveSitePhotoCoverValidator().Validate(new SetGraveSitePhotoCover(Guid.Empty)).Errors, error => error.ErrorCode == "photo.not_found");
        Assert.Contains(new DeleteGraveSitePhotoValidator().Validate(new DeleteGraveSitePhoto(Guid.Empty)).Errors, error => error.ErrorCode == "photo.not_found");
        Assert.True(new DeleteGraveSitePhotoValidator().Validate(new DeleteGraveSitePhoto(Guid.CreateVersion7())).IsValid);
    }

    private sealed class World
    {
        public World()
        {
            Caller = Callers.For(TenantId, MembershipRole.FieldWorker);
            Site = GraveSite.Place(TenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
            Layouts = Substitute.For<ILayoutRepository>();
            Layouts.FindGraveSiteAsync(Site.Id, Arg.Any<CancellationToken>()).Returns(Site);
            Photos = Substitute.For<IGraveSitePhotoRepository>();
            Photos.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSitePhoto>());
            Images = Substitute.For<IImageProcessor>();
            Images.Process(Arg.Any<byte[]>()).Returns(Processed);
            Storage = Substitute.For<IFileStorage>();
            Storage.PutAsync(TenantId, Processed.Full, Arg.Any<CancellationToken>()).Returns("full-key");
            Storage.PutAsync(TenantId, Processed.Thumbnail, Arg.Any<CancellationToken>()).Returns("thumb-key");
        }

        public Guid TenantId { get; } = Guid.CreateVersion7();

        public ICallerContext Caller { get; }

        public GraveSite Site { get; }

        public ILayoutRepository Layouts { get; }

        public IGraveSitePhotoRepository Photos { get; }

        public IImageProcessor Images { get; }

        public IFileStorage Storage { get; }

        public IUnitOfWork Unit { get; } = Substitute.For<IUnitOfWork>();

        public FakeTimeProvider Clock { get; } = new(Now);

        public ProcessedImage Processed { get; } = new([1], [2], 1600, 1200);

        public AddGraveSitePhotoHandler Add() => new(Caller, Layouts, Photos, Images, Storage, Unit, Clock);

        public GraveSitePhoto Photo(bool cover)
        {
            var photo = GraveSitePhoto.Take(TenantId, Site.Id, new GraveSitePhotoFiles("full-key", "thumb-key", 1600, 1200), GraveSitePhotoDetails.Create(null, null, DateOnly.FromDateTime(Now.UtcDateTime)), Now);
            if (cover)
                photo.MarkCover();
            return photo;
        }

        public void Has(params GraveSitePhoto[] photos)
        {
            Photos.ListAsync(Site.Id, Arg.Any<CancellationToken>()).Returns(photos);
            foreach (var photo in photos)
                Photos.FindAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);
        }

        private static GeoPolygon Box() =>
            GeoPolygon.Create([
                new GeoPoint(16, 45),
                new GeoPoint(16.001, 45),
                new GeoPoint(16.001, 45.001),
                new GeoPoint(16, 45.001),
                new GeoPoint(16, 45),
            ]);
    }
}
