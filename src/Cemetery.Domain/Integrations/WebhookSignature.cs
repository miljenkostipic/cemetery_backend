using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cemetery.Domain.Integrations;

public static class WebhookSignature
{
    public const string TimestampHeader = "X-Cemetery-Timestamp";

    public const string Header = "X-Cemetery-Signature";

    public const string Prefix = "sha256=";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static string Timestamp(DateTimeOffset at) =>
        at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static string Sign(string secret, string timestamp, string body)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var material = Encoding.UTF8.GetBytes(timestamp + "." + body);
        return Prefix + Convert.ToHexString(HMACSHA256.HashData(key, material)).ToLowerInvariant();
    }

    public static bool Matches(string secret, string timestamp, string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Sign(secret, timestamp, body));
        var actual = Encoding.UTF8.GetBytes(signature.Trim());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
