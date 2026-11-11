using System.Net;
using System.Net.Http.Json;
using System.Text;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Integrations;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Register;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cemetery.Api.IntegrationTests;

internal static class Loopback
{
    public static Uri? Target { get; set; }
}

internal sealed class LoopbackHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Loopback.Target is { } target && request.RequestUri is { } uri)
            request.RequestUri = new Uri(target, uri.PathAndQuery);

        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class OutboxDeliveryTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task An_interment_writes_one_outbox_row_and_a_failed_save_writes_none()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var interment = await OfficeWorld.PostId(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        var stored = await RowsAsync(CancellationToken);
        var row = Assert.Single(stored);
        Assert.Equal("interment.recorded", row.Type);
        Assert.Contains(interment.ToString("D"), row.Payload, StringComparison.Ordinal);

        using var scope = Api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.CreateVersion7());
        var db = scope.ServiceProvider.GetRequiredService<CemeteryDbContext>();
        var organization = scope.ServiceProvider.GetRequiredService<ITenantContext>().OrganizationId!.Value;
        db.Add(Interment.Record(organization, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, IntermentKind.Coffin, new DateOnly(2026, 1, 2)));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken));

        Assert.Single(await RowsAsync(CancellationToken));
    }

    [Fact]
    public async Task A_signed_interment_event_reaches_a_local_listener()
    {
        using var listener = Listening.Start();
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var created = await OfficeWorld.Post(world.Clerk, "/api/v1/integrations/webhooks", new { url = "https://hooks.example.com/cemetery", @event = "interment.recorded" }, CancellationToken);
        var secret = created.GetProperty("secret").GetString() ?? "";
        await OfficeWorld.PostId(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        using (var scope = Api.CreateScope())
            await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(CancellationToken);

        var captured = await listener.Received.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        Assert.True(WebhookSignature.Matches(secret, captured.Timestamp, captured.Body, captured.Signature));
        Assert.Contains("interment.recorded", captured.Body, StringComparison.Ordinal);
        var deliveries = await OfficeWorld.Get(world.Clerk, "/api/v1/integrations/webhook-deliveries", CancellationToken);
        Assert.Contains("delivered", ApiAssert.Rows(deliveries).EnumerateArray().Select(item => item.GetProperty("status").GetString()));
        await OfficeWorld.Hidden(world.Other, "/api/v1/integrations/webhook-deliveries", null, CancellationToken);
    }

    [Fact]
    public async Task The_monri_sandbox_returns_a_hosted_page()
    {
        const string response = """{"payment_url":"https://ipgtest.monri.com/pay/abc","id":"sess-1"}""";
        using var listener = Listening.Start(response);
        using var scope = Api.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IEnumerable<IPaymentGateway>>().Single(item => item.Name == "monri");
        var draft = new CheckoutDraft(Guid.CreateVersion7(), Guid.CreateVersion7(), 25.00m, "EUR", "Upkeep", "https://api.example.com/callback");
        var session = await gateway.OpenAsync(draft, new MerchantCredentials("merchant-1", "secret-ok", true), CancellationToken);

        var captured = await listener.Received.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        Assert.Equal("/v2/payment/new", captured.Path);
        Assert.StartsWith("WP3-v2 ", captured.Authorization, StringComparison.Ordinal);
        Assert.Equal("https://ipgtest.monri.com/pay/abc", session.RedirectUrl);
    }

    [Fact]
    public async Task A_provider_callback_does_not_require_a_session()
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/v1/payments/callbacks/monri", new { }, CancellationToken);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<IReadOnlyList<OutboxRow>> RowsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(Api.OwnerConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT type, payload::text FROM cemetery.outbox_messages", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<OutboxRow>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new OutboxRow(reader.GetString(0), reader.GetString(1)));

        return rows;
    }

    private readonly record struct OutboxRow(string Type, string Payload);
}

internal sealed class Listening : IDisposable
{
    private readonly HttpListener listener;
    private readonly TaskCompletionSource<Captured> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Listening(HttpListener listener) => this.listener = listener;

    public Task<Captured> Received => received.Task;

    public static Listening Start(string? responseBody = null)
    {
        var port = FreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Loopback.Target = new Uri($"http://127.0.0.1:{port}/");
        var listening = new Listening(listener);
        _ = listening.Serve(responseBody);
        return listening;
    }

    public void Dispose()
    {
        Loopback.Target = null;
        listener.Close();
        received.TrySetCanceled();
    }

    private static int FreePort()
    {
        var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private async Task Serve(string? responseBody)
    {
        try
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            var captured = new Captured(
                context.Request.Url?.AbsolutePath ?? "",
                context.Request.Headers[WebhookSignature.Header] ?? "",
                context.Request.Headers[WebhookSignature.TimestampHeader] ?? "",
                context.Request.Headers["Authorization"] ?? "",
                body);
            var bytes = Encoding.UTF8.GetBytes(responseBody ?? "");
            context.Response.StatusCode = responseBody is null ? 204 : 200;
            context.Response.ContentType = "application/json";
            if (bytes.Length > 0)
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            context.Response.Close();
            received.TrySetResult(captured);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException)
        {
            received.TrySetCanceled();
        }
    }
}

internal readonly record struct Captured(string Path, string Signature, string Timestamp, string Authorization, string Body);
