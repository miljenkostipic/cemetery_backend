using System.Net.Http.Headers;
using System.Text;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cemetery.Infrastructure.Identity;

internal sealed record MailgunAccount(Uri Messages, string ApiKey, string From);

internal static class MailgunSettings
{
    private const string DefaultRegionUrl = "https://api.mailgun.net/v3";

    public static MailgunAccount? Read(IConfiguration configuration)
    {
        var key = configuration["MailgunSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var account = Create(
            key.Trim(),
            configuration["MailgunSettings:Domain"],
            configuration["MailgunSettings:FromEmail"],
            configuration["MailgunSettings:FromName"],
            configuration["MailgunSettings:RegionUrl"]);
        return account ?? throw new InvalidOperationException("MailgunSettings:Domain, MailgunSettings:FromEmail, MailgunSettings:FromName, and MailgunSettings:RegionUrl must identify a Mailgun account.");
    }

    private static MailgunAccount? Create(string key, string? domain, string? fromEmail, string? fromName, string? regionUrl)
    {
        if (string.IsNullOrWhiteSpace(domain) || Uri.CheckHostName(domain) != UriHostNameType.Dns)
            return null;
        if (!EmailAddress.TryParse(fromEmail, out var sender) || !TryFrom(fromName, sender.Value, out var from))
            return null;
        if (!TryMessages(regionUrl, domain, out var messages))
            return null;

        return new MailgunAccount(messages, key, from);
    }

    private static bool TryFrom(string? fromName, string email, out string from)
    {
        from = "";
        if (string.IsNullOrWhiteSpace(fromName))
            return false;

        var name = fromName.Trim();
        if (name.Contains('\r') || name.Contains('\n') || name.Contains('<') || name.Contains('>'))
            return false;

        from = string.Concat(name, " <", email, ">");
        return true;
    }

    private static bool TryMessages(string? regionUrl, string domain, out Uri messages)
    {
        messages = null!;
        var root = string.IsNullOrWhiteSpace(regionUrl) ? DefaultRegionUrl : regionUrl.Trim();
        if (!Uri.TryCreate(root, UriKind.Absolute, out var region) || !IsMailgunRegion(region))
            return false;

        messages = new UriBuilder(region.Scheme, region.Host) { Path = $"/v3/{domain}/messages" }.Uri;
        return true;
    }

    private static bool IsMailgunRegion(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.Query)
        && IsVersionPath(uri.AbsolutePath)
        && IsMailgunHost(uri.Host);

    private static bool IsVersionPath(string path) =>
        path == "/" || path.Equals("/v3", StringComparison.OrdinalIgnoreCase) || path.Equals("/v3/", StringComparison.OrdinalIgnoreCase);

    private static bool IsMailgunHost(string host) =>
        host.Equals("api.mailgun.net", StringComparison.OrdinalIgnoreCase)
        || host.Equals("api.eu.mailgun.net", StringComparison.OrdinalIgnoreCase);
}

internal sealed class MailgunEmailSender(HttpClient http, MailgunAccount account, ILogger<MailgunEmailSender> logger) : IEmailSender
{
    public Task SendInvitationAsync(EmailAddress recipient, string acceptUrl, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Pozivnica", $"Pozvani ste u organizaciju. Otvorite poveznicu: {acceptUrl}", cancellationToken);

    public Task SendPasswordResetAsync(EmailAddress recipient, string resetUrl, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Nova lozinka", $"Postavite novu lozinku: {resetUrl}", cancellationToken);

    private async Task SendAsync(EmailAddress recipient, string subject, string text, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(recipient, subject, text);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            Reject(response.StatusCode);

        await LogQueuedAsync(recipient, subject, response, cancellationToken).ConfigureAwait(false);
    }

    private void Reject(System.Net.HttpStatusCode status)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("Mailgun rejected mail from {From} with status {StatusCode}", account.From, (int)status);
        throw new InvalidOperationException("Mailgun rejected the message.");
    }

    private async Task LogQueuedAsync(EmailAddress recipient, string subject, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var provider = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Mail queued for {Recipient} from {From} with subject {Subject}. Mailgun status {StatusCode}. {ProviderResponse}",
            recipient.Value,
            account.From,
            subject,
            (int)response.StatusCode,
            Trim(provider));
    }

    private static string Trim(string provider) => provider.Length <= 180 ? provider : provider[..180];

    private HttpRequestMessage CreateRequest(EmailAddress recipient, string subject, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, account.Messages);
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Concat("api:", account.ApiKey)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        request.Content = new FormUrlEncodedContent(Fields(recipient, subject, text));
        return request;
    }

    private Dictionary<string, string> Fields(EmailAddress recipient, string subject, string text) => new()
    {
        ["from"] = account.From,
        ["to"] = recipient.Value,
        ["subject"] = subject,
        ["text"] = text,
    };
}
