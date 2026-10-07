using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;

namespace Cemetery.Domain.Tests;

public sealed class OfficeRequestTests
{
    [Fact]
    public void A_request_keeps_the_trimmed_message()
    {
        var request = OfficeRequest.Send(Guid.CreateVersion7(), Guid.CreateVersion7(), "  Please call  ", null, new DateOnly(2026, 10, 7));

        Assert.Equal("Please call", request.Message);
        Assert.Null(request.GraveSiteId);
        Assert.Equal(new DateOnly(2026, 10, 7), request.CreatedOn);
    }

    [Fact]
    public void A_blank_message_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            OfficeRequest.Send(Guid.CreateVersion7(), Guid.CreateVersion7(), "  ", null, new DateOnly(2026, 10, 7)));

        Assert.Equal("request.message_invalid", error.Code);
    }

    [Fact]
    public void An_empty_grave_site_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            OfficeRequest.Send(Guid.CreateVersion7(), Guid.CreateVersion7(), "Please call", Guid.Empty, new DateOnly(2026, 10, 7)));

        Assert.Equal("grave_site.not_found", error.Code);
    }
}
