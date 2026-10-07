using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Tests;

public sealed class PublicListingTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly DateTimeOffset At = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_person_is_public_only_when_the_register_is_published_and_the_grave_is_not_hidden()
    {
        var recent = new DateOnly(2026, 10, 1);
        Assert.False(PublicListing.IsListed(new ListingFacts(false, false, recent, 0, Today)));
        Assert.False(PublicListing.IsListed(new ListingFacts(true, true, recent, 0, Today)));
        Assert.True(PublicListing.IsListed(new ListingFacts(true, false, recent, 0, Today)));
        Assert.False(PublicListing.IsListed(new ListingFacts(true, false, null, 30, Today)));
        Assert.False(PublicListing.IsListed(new ListingFacts(true, false, recent, 30, Today)));
        Assert.True(PublicListing.IsListed(new ListingFacts(true, false, new DateOnly(2020, 1, 1), 30, Today)));
        Assert.False(PublicListing.IsListed(new ListingFacts(true, false, DateOnly.MaxValue, PublicListing.HideRecentDeathsMaxDays, Today)));
    }

    [Fact]
    public void Hide_days_names_years_epitaphs_and_photos_fail_closed()
    {
        Assert.Equal(0, PublicListing.RequireHideDays(0));
        var days = Assert.Throws<DomainRuleException>(() => PublicListing.RequireHideDays(-1));
        Assert.Equal("visibility.days_invalid", days.Code);
        var far = Assert.Throws<DomainRuleException>(() => PublicListing.RequireHideDays(PublicListing.HideRecentDeathsMaxDays + 1));
        Assert.Equal("visibility.days_invalid", far.Code);

        Assert.Equal("Ana Horvat", PublicListing.RequireSearchName(" Ana Horvat "));
        var name = Assert.Throws<DomainRuleException>(() => PublicListing.RequireSearchName(" A "));
        Assert.Equal("search.name_invalid", name.Code);
        var wildcard = Assert.Throws<DomainRuleException>(() => PublicListing.RequireSearchName("Ana%"));
        Assert.Equal("search.name_invalid", wildcard.Code);

        Assert.Equal((1990, 2000), PublicListing.RequireYears(1990, 2000));
        var years = Assert.Throws<DomainRuleException>(() => PublicListing.RequireYears(2001, 1990));
        Assert.Equal("search.year_invalid", years.Code);

        Assert.Null(PublicListing.EpitaphOf("  "));
        Assert.Equal("In memory", PublicListing.EpitaphOf(" In memory "));
        var epitaph = Assert.Throws<DomainRuleException>(() => PublicListing.EpitaphOf(new string('a', PublicListing.EpitaphMaxLength + 1)));
        Assert.Equal("memorial.epitaph_invalid", epitaph.Code);
        Assert.Equal("https://example.com/a.jpg", PublicListing.PhotoOf(" https://example.com/a.jpg "));
        Assert.Null(PublicListing.PhotoOf(null));
        var photo = Assert.Throws<DomainRuleException>(() => PublicListing.PhotoOf("javascript:alert(1)"));
        Assert.Equal("memorial.photo_invalid", photo.Code);
    }

    [Fact]
    public void Publishing_hiding_and_a_memorial_are_recorded_on_the_aggregates()
    {
        var organization = Organization.Create("Pilot", "pilot", At);
        organization.PublishRegister(14);
        Assert.True(organization.PublishesRegister);
        Assert.Equal(14, organization.HideRecentDeathsDays);
        organization.WithdrawRegister();
        Assert.False(organization.PublishesRegister);

        var site = GraveSite.Place(organization.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Ring());
        site.HideFromPublic();
        Assert.True(site.HiddenFromPublic);
        site.ShowOnPublicMap();
        Assert.False(site.HiddenFromPublic);

        var person = Deceased.Record(organization.Id, Guid.CreateVersion7(), "Ana", "Horvat", null, new DateOnly(2020, 1, 1), At);
        person.DescribeMemorial(" Rest ", "http://example.com/a.jpg");
        Assert.Equal("Rest", person.Epitaph);
        Assert.Equal("http://example.com/a.jpg", person.PhotoUrl);
        Assert.True(AuditActions.IsKnown(AuditActions.RegisterPublished));
        Assert.True(AuditActions.IsKnown(AuditActions.MemorialDescribed));
    }

    private static GeoPolygon Ring() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}
