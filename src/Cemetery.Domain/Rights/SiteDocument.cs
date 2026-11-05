using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Rights;

public enum DocumentKind
{
    Contract = 1,
    Permit = 2,
    Scan = 3,
    Other = 4,
}

public static class DocumentKinds
{
    public static DocumentKind Parse(string? kind) => kind switch
    {
        null or "" or "other" => DocumentKind.Other,
        "contract" => DocumentKind.Contract,
        "permit" => DocumentKind.Permit,
        "scan" => DocumentKind.Scan,
        _ => throw new DomainRuleException("document.kind_invalid"),
    };

    public static string ToCode(DocumentKind kind) => kind switch
    {
        DocumentKind.Contract => "contract",
        DocumentKind.Permit => "permit",
        DocumentKind.Scan => "scan",
        DocumentKind.Other => "other",
        _ => throw new DomainRuleException("document.kind_invalid"),
    };
}

public static class DocumentBytes
{
    public const int MaxBytes = 10_000_000;

    public const int NameMaxLength = 120;

    public static (string FileName, string ContentType, byte[] Content) Decode(string? fileName, string? contentType, string? encoded)
    {
        var name = RequireName(fileName);
        var type = RequireType(contentType);
        var bytes = DecodeBase64(encoded);
        if (!Matches(type, bytes))
            throw new DomainRuleException("document.type_invalid");

        return (name, type, bytes);
    }

    private static string RequireName(string? fileName)
    {
        var name = fileName?.Trim() ?? "";
        if (name.Length is < 1 or > NameMaxLength || name.Contains('/') || name.Contains('\\'))
            throw new DomainRuleException("document.name_invalid");

        return name;
    }

    private static string RequireType(string? contentType) => contentType switch
    {
        "application/pdf" or "image/jpeg" or "image/png" => contentType,
        _ => throw new DomainRuleException("document.type_invalid"),
    };

    private static byte[] DecodeBase64(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new DomainRuleException("document.empty");

        try
        {
            var bytes = Convert.FromBase64String(encoded.Trim());
            if (bytes.Length is < 1 or > MaxBytes)
                throw new DomainRuleException("document.too_large");

            return bytes;
        }
        catch (FormatException)
        {
            throw new DomainRuleException("document.type_invalid");
        }
    }

    private static bool Matches(string contentType, byte[] bytes) => contentType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        _ => bytes.Length >= 4 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F',
    };
}

public sealed class SiteDocument : TenantEntity
{
    private SiteDocument()
    {
        FileName = "";
        ContentType = "";
        StorageKey = "";
    }

    public Guid GraveSiteId { get; private set; }

    public Guid? GraveRightId { get; private set; }

    public DocumentKind Kind { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public int ByteSize { get; private set; }

    public string StorageKey { get; private set; }

    public static SiteDocument Store(
        Guid tenantId,
        Guid graveSiteId,
        Guid? graveRightId,
        DocumentKind kind,
        string fileName,
        string contentType,
        int byteSize,
        string storageKey)
    {
        if (tenantId == Guid.Empty || graveSiteId == Guid.Empty || string.IsNullOrWhiteSpace(storageKey))
            throw new DomainRuleException("document.invalid");

        return new SiteDocument
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            GraveSiteId = graveSiteId,
            GraveRightId = graveRightId,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            ByteSize = byteSize,
            StorageKey = storageKey,
        };
    }
}
