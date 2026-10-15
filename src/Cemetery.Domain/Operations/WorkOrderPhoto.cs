using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public static class PhotoBytes
{
    public const int MaxBytes = 5_000_000;

    public static (string ContentType, byte[] Content) Decode(string? contentType, string? encoded)
    {
        var type = RequireType(contentType);
        var bytes = DecodeBase64(encoded);
        if (!Matches(type, bytes))
            throw new DomainRuleException("photo.type_invalid");
        return (type, bytes);
    }

    private static string RequireType(string? contentType) => contentType switch
    {
        "image/jpeg" or "image/png" => contentType,
        _ => throw new DomainRuleException("photo.type_invalid"),
    };

    private static byte[] DecodeBase64(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new DomainRuleException("photo.empty");

        var compact = encoded.Trim();
        if (compact.Length > MaxBase64Chars())
            throw new DomainRuleException("photo.too_large");

        return Converted(compact);
    }

    private static byte[] Converted(string compact)
    {
        try
        {
            var bytes = Convert.FromBase64String(compact);
            if (bytes.Length > MaxBytes)
                throw new DomainRuleException("photo.too_large");
            return bytes;
        }
        catch (FormatException)
        {
            throw new DomainRuleException("photo.type_invalid");
        }
    }

    private static bool Matches(string contentType, byte[] bytes) =>
        contentType == "image/jpeg" ? IsJpeg(bytes) : IsPng(bytes);

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89
        && bytes[1] == 0x50
        && bytes[2] == 0x4E
        && bytes[3] == 0x47
        && bytes[4] == 0x0D
        && bytes[5] == 0x0A
        && bytes[6] == 0x1A
        && bytes[7] == 0x0A;

    private static int MaxBase64Chars() => 4 * ((MaxBytes + 2) / 3);
}

public sealed class WorkOrderPhoto : TenantEntity
{
    private WorkOrderPhoto()
    {
        ContentType = "";
        Content = [];
    }

    public Guid WorkOrderId { get; private set; }

    public string ContentType { get; private set; }

    public byte[] Content { get; private set; }

    public static WorkOrderPhoto Record(Guid tenantId, Guid workOrderId, string? contentType, string? encoded)
    {
        if (tenantId == Guid.Empty || workOrderId == Guid.Empty)
            throw new DomainRuleException("work_order.not_found");

        var decoded = PhotoBytes.Decode(contentType, encoded);
        return Create(tenantId, workOrderId, decoded.ContentType, decoded.Content);
    }

    private static WorkOrderPhoto Create(Guid tenantId, Guid workOrderId, string contentType, byte[] content) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            ContentType = contentType,
            Content = content,
        };
}
