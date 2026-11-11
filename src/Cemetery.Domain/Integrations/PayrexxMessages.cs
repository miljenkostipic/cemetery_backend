using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Integrations;

public static class PayrexxMessages
{
    public const string SignatureHeader = "X-API-Signature";

    public static string NewGateway(CheckoutDraft draft)
    {
        var body = new JsonObject
        {
            ["amount"] = MonriMessages.Minor(draft.Amount),
            ["currency"] = draft.Currency,
            ["purpose"] = draft.Description,
            ["referenceId"] = PaymentOrder.Number(draft.TenantId, draft.PaymentId),
            ["successRedirectUrl"] = draft.CallbackUrl,
            ["failedRedirectUrl"] = draft.CallbackUrl,
        };
        return body.ToJsonString();
    }

    public static HostedCheckout ReadGateway(string json)
    {
        var row = Node(json)["data"]?[0] ?? throw new DomainRuleException("payment.provider_invalid");
        return new HostedCheckout(PaymentLink.Require(row["link"]?.GetValue<string>()));
    }

    public static ProviderNotice ReadCallback(string json, string? signature, string secret)
    {
        if (!DigestMatches(secret, json, signature))
            throw new DomainRuleException("payment.signature_invalid");

        var transaction = Node(json)["transaction"] ?? throw new DomainRuleException("payment.not_found");
        var order = transaction["referenceId"]?.GetValue<string>();
        if (!PaymentOrder.TryRead(order, out var tenantId, out var paymentId))
            throw new DomainRuleException("payment.not_found");

        var approved = transaction["status"]?.GetValue<string>() == "confirmed";
        var transactionId = transaction["uuid"]?.GetValue<string>() ?? "";
        if (approved)
            transactionId = ProviderTransaction.Require(transactionId);

        return new ProviderNotice(tenantId, paymentId, transactionId, approved);
    }

    public static string Digest(string secret, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private static bool DigestMatches(string secret, string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Digest(secret, body));
        var actual = Encoding.UTF8.GetBytes(signature.Trim());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static JsonNode Node(string json) =>
        JsonNode.Parse(json) ?? throw new DomainRuleException("payment.provider_invalid");
}
