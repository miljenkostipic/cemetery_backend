using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Application.Integrations;
using Cemetery.Application.Launch;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Launch;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class OutboxDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_failing_handler_is_retried_and_the_error_is_kept()
    {
        var message = OutboxMessage.Capture(Guid.CreateVersion7(), new IntermentRecorded(Guid.CreateVersion7()), Now);
        var store = new MemoryOutbox(message);
        var handler = new Flaky(1);
        var clock = new MutableClock(Now);
        var dispatcher = new OutboxDispatcher(store, handler, new TenantHolder(), new HintHolder(), clock);

        Assert.Equal(0, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Equal("boom", message.LastError);
        Assert.Null(message.ProcessedAt);

        clock.Now = Now.AddSeconds(OutboxMessage.FirstRetrySeconds);
        Assert.Equal(1, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(2, store.Saves);
    }

    [Fact]
    public async Task A_message_stops_after_the_attempt_limit()
    {
        var message = OutboxMessage.Capture(Guid.CreateVersion7(), new IntermentRecorded(Guid.CreateVersion7()), Now);
        var handler = new Flaky(OutboxMessage.MaxAttempts + 1);
        var clock = new MutableClock(Now);
        var dispatcher = new OutboxDispatcher(new MemoryOutbox(message), handler, new TenantHolder(), new HintHolder(), clock);

        for (var attempt = 0; attempt < OutboxMessage.MaxAttempts; attempt++)
        {
            clock.Now = Now.AddSeconds(attempt == 0 ? 0 : OutboxMessage.DelaySeconds(attempt));
            await dispatcher.DispatchAsync(CancellationToken.None);
        }

        clock.Now = Now.AddDays(3);
        await dispatcher.DispatchAsync(CancellationToken.None);

        Assert.Equal(OutboxMessage.MaxAttempts, handler.Calls);
        Assert.Null(message.ProcessedAt);
    }

    private sealed class MemoryOutbox(OutboxMessage message) : IOutboxStore
    {
        public int Saves { get; private set; }

        public Task<IReadOnlyList<OutboxMessage>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(message.IsDue(now) ? [message] : []);

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class Flaky(int failures) : IOutboxHandler
    {
        public int Calls { get; private set; }

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failures)
                throw new InvalidOperationException("boom");

            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TenantHolder : ITenantContext
    {
        public Guid? OrganizationId { get; private set; }

        public void Use(Guid organizationId) => OrganizationId = organizationId;
    }

    private sealed class HintHolder : ISessionHints
    {
        public string InvitationHash { get; set; } = "";

        public bool PublicRead { get; set; }

        public bool OutboxDispatch { get; set; }
    }
}

public sealed class WebhookDeliveryHandlerTests
{
    [Fact]
    public async Task A_matching_subscription_is_posted_and_logged()
    {
        var tenant = Guid.CreateVersion7();
        var subscription = WebhookSubscription.Register(tenant, "https://hooks.example.com/cemetery", "interment.recorded", "secret-ok");
        var message = OutboxMessage.Capture(tenant, new IntermentRecorded(Guid.CreateVersion7()), DateTimeOffset.UnixEpoch);
        var webhooks = Substitute.For<IWebhookRepository>();
        webhooks.ListByEventAsync(message.Type, Arg.Any<CancellationToken>()).Returns([subscription]);
        webhooks.WasDeliveredAsync(subscription.Id, message.Id, Arg.Any<CancellationToken>()).Returns(false);
        var poster = Substitute.For<IWebhookPoster>();
        poster.PostAsync(subscription.Url, subscription.Secret, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(204);
        var handler = new WebhookDeliveryHandler(webhooks, poster, TimeProvider.System);

        await handler.HandleAsync(message, CancellationToken.None);

        webhooks.Received(1).AddDelivery(Arg.Is<WebhookDelivery>(delivery => delivery.Status == WebhookDeliveryStatus.Delivered));
    }

    [Fact]
    public async Task A_failed_post_is_logged_and_thrown()
    {
        var tenant = Guid.CreateVersion7();
        var subscription = WebhookSubscription.Register(tenant, "https://hooks.example.com/cemetery", "interment.recorded", "secret-ok");
        var message = OutboxMessage.Capture(tenant, new IntermentRecorded(Guid.CreateVersion7()), DateTimeOffset.UnixEpoch);
        var webhooks = Substitute.For<IWebhookRepository>();
        webhooks.ListByEventAsync(message.Type, Arg.Any<CancellationToken>()).Returns([subscription]);
        var poster = Substitute.For<IWebhookPoster>();
        poster.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(500);
        var handler = new WebhookDeliveryHandler(webhooks, poster, TimeProvider.System);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal("webhook.delivery_failed", error.Code);
        webhooks.Received(1).AddDelivery(Arg.Is<WebhookDelivery>(delivery => delivery.Status == WebhookDeliveryStatus.Failed));
    }
}
