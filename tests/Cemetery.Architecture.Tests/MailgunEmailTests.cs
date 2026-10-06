using System.Net;
using System.Text;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cemetery.Architecture.Tests;

public sealed class MailgunEmailTests
{
    [Fact]
    public void Missing_api_key_keeps_mail_unconfigured()
    {
        var configuration = Config(new Dictionary<string, string?>
        {
            ["MailgunSettings:Domain"] = "mg.example.com",
            ["MailgunSettings:FromEmail"] = "noreply@example.com",
            ["MailgunSettings:FromName"] = "Matica Support",
        });

        Assert.Null(MailgunSettings.Read(configuration));
    }

    [Fact]
    public void European_account_uses_the_eu_messages_endpoint()
    {
        var account = MailgunSettings.Read(Config(Account("https://api.eu.mailgun.net/v3")));

        Assert.NotNull(account);
        Assert.Equal(new Uri("https://api.eu.mailgun.net/v3/mg.example.com/messages"), account.Messages);
        Assert.Equal("Matica Support <noreply@example.com>", account.From);
    }

    [Fact]
    public void Rejects_an_api_key_whose_endpoint_is_not_mailgun()
    {
        var configuration = Config(Account("https://example.com"));

        var error = Assert.Throws<InvalidOperationException>(() => MailgunSettings.Read(configuration));

        Assert.Contains("Mailgun", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_a_reset_with_basic_auth_and_the_recipient()
    {
        var account = new MailgunAccount(new Uri("https://api.mailgun.net/v3/mg.example.com/messages"), "secret-key", "noreply@example.com");
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var sender = new MailgunEmailSender(http, account, NullLogger<MailgunEmailSender>.Instance);

        await sender.SendPasswordResetAsync(EmailAddress.Parse("ada@example.com"), "https://localhost/password-reset?token=1", CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal(account.Messages, handler.Request.RequestUri);
        Assert.Equal("Basic", handler.Request.Headers.Authorization?.Scheme);
        var expected = Convert.ToBase64String(Encoding.ASCII.GetBytes("api:secret-key"));
        Assert.Equal(expected, handler.Request.Headers.Authorization?.Parameter);
        Assert.Contains("to=ada%40example.com", handler.Body, StringComparison.Ordinal);
        Assert.Contains("subject=Nova+lozinka", handler.Body, StringComparison.Ordinal);
        Assert.Contains("from=noreply%40example.com", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_failed_mailgun_response()
    {
        var account = new MailgunAccount(new Uri("https://api.mailgun.net/v3/mg.example.com/messages"), "secret-key", "noreply@example.com");
        using var http = new HttpClient(new RecordingHandler { Status = HttpStatusCode.Unauthorized });
        var sender = new MailgunEmailSender(http, account, NullLogger<MailgunEmailSender>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendInvitationAsync(EmailAddress.Parse("ada@example.com"), "https://localhost/invitations/accept?token=1", CancellationToken.None));
    }

    private static Dictionary<string, string?> Account(string baseUrl) => new()
    {
        ["MailgunSettings:ApiKey"] = "secret-key",
        ["MailgunSettings:Domain"] = "mg.example.com",
        ["MailgunSettings:FromEmail"] = "noreply@example.com",
        ["MailgunSettings:FromName"] = "Matica Support",
        ["MailgunSettings:RegionUrl"] = baseUrl,
    };

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = "";

        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(Status);
        }
    }
}
