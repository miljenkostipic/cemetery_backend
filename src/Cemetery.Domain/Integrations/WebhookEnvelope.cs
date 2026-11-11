using System.Globalization;
using System.Text.Json.Nodes;

namespace Cemetery.Domain.Integrations;

public static class WebhookEnvelope
{
    public static string Write(OutboxMessage message)
    {
        var body = new JsonObject
        {
            ["id"] = message.Id.ToString("D"),
            ["type"] = message.Type,
            ["occurredAt"] = message.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
            ["data"] = JsonNode.Parse(message.Payload),
        };
        return body.ToJsonString();
    }
}
