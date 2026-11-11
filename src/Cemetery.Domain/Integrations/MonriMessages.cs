using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Integrations;

public static class MonriMessages
{
    public const string SignatureHeader = "Authorization";

    public const string CallbackScheme = "WP3-callback";

    public static long Minor(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    public static string NewPayment(CheckoutDraft draft)
    {
        var body = new JsonObject
        {
            ["amount"] = Minor(draft.Amount),
            ["order_number"] = PaymentOrder.Number(draft.TenantId, draft.PaymentId),
            ["currency"] = draft.Currency,
            ["transaction_type"] = "purchase",
            ["order_info"] = draft.Description,
            ["scenario"] = "charge",
            ["callback_url"] = draft.CallbackUrl,
        };
        return body.ToJsonString();
    }

    public static string Authorization(string token, string key, string timestamp, string body) =>
        $"WP3-v2 {token} {timestamp} {Hash(key + timestamp + token + body)}";

    public static string CallbackDigest(string key, string body) => Hash(key + body);

    public static HostedCheckout ReadSession(string json)
    {
        var url = Node(json)["payment_url"]?.GetValue<string>();
        return new HostedCheckout(PaymentLink.Require(url));
    }

    public static ProviderNotice ReadCallback(string json, string? signature, string secret)
    {
        if (!DigestMatches(secret, json, signature))
            throw new DomainRuleException("payment.signature_invalid");

        return Notice(Node(json), "approved");
    }

    private static bool DigestMatches(string secret, string body, string? signature)
    {
        const string prefix = CallbackScheme + " ";
        if (signature is null || !signature.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var expected = Encoding.UTF8.GetBytes(CallbackDigest(secret, body));
        var actual = Encoding.UTF8.GetBytes(signature[prefix.Length..].Trim());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static ProviderNotice Notice(JsonNode node, string approvedStatus)
    {
        var order = node["order_number"]?.GetValue<string>();
        if (!PaymentOrder.TryRead(order, out var tenantId, out var paymentId))
            throw new DomainRuleException("payment.not_found");

        var approved = node["status"]?.GetValue<string>() == approvedStatus;
        var transactionId = node["id"]?.GetValue<string>() ?? "";
        if (approved)
            transactionId = ProviderTransaction.Require(transactionId);

        return new ProviderNotice(tenantId, paymentId, transactionId, approved);
    }

    private static JsonNode Node(string json) =>
        JsonNode.Parse(json) ?? throw new DomainRuleException("payment.provider_invalid");

    private static string Hash(string material) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
}

public static class ProviderTransaction
{
    public const int MaxLength = 80;

    public const string Invalid = "payment.reference_invalid";

    public static string Require(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0 || text.Length > MaxLength || BillingText.Unsafe(text))
            throw new DomainRuleException(Invalid);

        return text;
    }
}
