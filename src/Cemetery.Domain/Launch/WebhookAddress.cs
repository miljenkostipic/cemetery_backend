using System.Net;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Launch;

public static class WebhookEvents
{
    public const string IntermentRecorded = "interment.recorded";

    public const string PaymentReceived = "payment.received";

    public const string FuneralScheduled = "funeral.scheduled";

    public static bool IsKnown(string? value) =>
        value is IntermentRecorded or PaymentReceived or FuneralScheduled;
}

public static class WebhookAddress
{
    public const int MaxLength = 200;

    public static string Require(string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength || HasControl(trimmed))
            throw new DomainRuleException("webhook.url_invalid");

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || !IsHttps(uri) || Blocked(Host(uri)))
            throw new DomainRuleException("webhook.url_invalid");

        var canonical = uri.AbsoluteUri;
        if (canonical.Length > MaxLength)
            throw new DomainRuleException("webhook.url_invalid");

        return canonical;
    }

    private static bool IsHttps(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && uri.Host.Length > 0;

    private static string Host(Uri uri) => uri.IdnHost.TrimEnd('.').ToLowerInvariant();

    private static bool Blocked(string host)
    {
        if (host.Length == 0 || host is "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || host.EndsWith(".local", StringComparison.Ordinal))
            return true;
        if (host.Contains(':', StringComparison.Ordinal) || IPAddress.TryParse(host, out _))
            return true;

        var parts = host.Split('.');
        return parts.Length == 4 && Array.TrueForAll(parts, static part => part.Length > 0 && AllDigits(part));
    }

    private static bool AllDigits(string part)
    {
        foreach (var character in part)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return true;
    }

    private static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (character < ' ' || character == '\u007f')
                return true;
        }

        return false;
    }
}
