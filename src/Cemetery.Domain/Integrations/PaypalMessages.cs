using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Integrations;

public static class PaypalMessages
{
    public const string SignatureHeader = "Paypal-Transmission-Sig";

    public static string NewOrder(CheckoutDraft draft)
    {
        var body = new JsonObject
        {
            ["intent"] = "CAPTURE",
            ["purchase_units"] = new JsonArray(new JsonObject
            {
                ["custom_id"] = PaymentOrder.Number(draft.TenantId, draft.PaymentId),
                ["description"] = draft.Description,
                ["amount"] = new JsonObject
                {
                    ["currency_code"] = draft.Currency,
                    ["value"] = FeeAmount.Text(draft.Amount),
                },
            }),
        };
        return body.ToJsonString();
    }

    public static HostedCheckout ReadOrder(string json)
    {
        var links = Node(json)["links"]?.AsArray() ?? [];
        foreach (var link in links)
        {
            if (link?["rel"]?.GetValue<string>() == "approve")
                return new HostedCheckout(PaymentLink.Require(link["href"]?.GetValue<string>()));
        }

        throw new DomainRuleException("payment.provider_invalid");
    }

    public static ProviderNotice ReadCallback(string json, string? signature, string secret)
    {
        if (!DigestMatches(secret, json, signature))
            throw new DomainRuleException("payment.signature_invalid");

        var root = Node(json);
        var resource = root["resource"] ?? throw new DomainRuleException("payment.not_found");
        var order = resource["custom_id"]?.GetValue<string>();
        if (!PaymentOrder.TryRead(order, out var tenantId, out var paymentId))
            throw new DomainRuleException("payment.not_found");

        var approved = root["event_type"]?.GetValue<string>() == "PAYMENT.CAPTURE.COMPLETED";
        var transactionId = resource["id"]?.GetValue<string>() ?? "";
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
