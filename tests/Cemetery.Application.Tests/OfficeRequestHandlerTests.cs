using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Rights;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class OfficeRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_lists_office_requests()
    {
        var tenant = Guid.CreateVersion7();
        var author = Guid.CreateVersion7();
        var stored = OfficeRequest.Send(tenant, author, "Please call", null, new DateOnly(2026, 10, 7));
        var requests = Substitute.For<IOfficeRequestRepository>();
        requests.PageAsync(Arg.Any<string?>(), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>()).Returns(new Page<OfficeRequest>([stored], null));
        var handler = new ListOfficeRequestsHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), requests);

        var listed = await handler.Handle(new ListOfficeRequests(), CancellationToken.None);

        Assert.Equal([stored.Id], listed.Items.Select(item => item.Id));
        Assert.Equal("Please call", listed.Items[0].Message);
    }

    [Fact]
    public async Task A_member_sends_a_request()
    {
        var requests = Substitute.For<IOfficeRequestRepository>();
        var handler = new SendOfficeRequestHandler(
            Callers.For(Guid.CreateVersion7(), MembershipRole.GraveHolder),
            Substitute.For<ILayoutRepository>(),
            requests,
            Substitute.For<IUnitOfWork>(),
            Clock());

        var created = await handler.Handle(new SendOfficeRequest(" Please call ", null), CancellationToken.None);

        Assert.Equal("Please call", created.Message);
        Assert.Equal(new DateOnly(2026, 10, 7), created.CreatedOn);
        await requests.Received(1).AddAsync(Arg.Any<OfficeRequest>(), Arg.Any<CancellationToken>());
    }




    private static TimeProvider Clock()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        return clock;
    }
}
