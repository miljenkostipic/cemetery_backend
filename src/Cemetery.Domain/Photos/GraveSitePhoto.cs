using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Photos;

/// <summary>An uploaded grave photo before processing: one of the accepted image types, at most <see cref="MaxBytes"/>.</summary>
public static class GraveSitePhotoBytes
{
    public const int MaxBytes = 15 * 1024 * 1024;

    public static byte[] Decode(string? contentType, string? encoded)
    {
        var type = RequireType(contentType);
        var bytes = DecodeBase64(encoded);
        if (!Matches(type, bytes))
            throw new DomainRuleException("photo.type_invalid");
        return bytes;
    }

    private static string RequireType(string? contentType) => contentType switch
    {
        "image/jpeg" or "image/png" or "image/webp" => contentType,
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
        // At most MaxBase64Chars characters never decode to more than MaxBytes bytes.
        try
        {
            return Convert.FromBase64String(compact);
        }
        catch (FormatException)
        {
            throw new DomainRuleException("photo.type_invalid");
        }
    }

    private static bool Matches(string contentType, byte[] bytes) => contentType switch
    {
        "image/jpeg" => IsJpeg(bytes),
        "image/png" => IsPng(bytes),
        _ => IsWebp(bytes),
    };

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(PngSignature);

    private static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static int MaxBase64Chars() => 4 * ((MaxBytes + 2) / 3);
}

/// <summary>What a clerk can say about a photo: a short caption and the day it was taken (never in the future).</summary>
public sealed record GraveSitePhotoDetails(string? Caption, DateOnly? TakenOn)
{
    public const int CaptionMaxLength = 300;

    public static GraveSitePhotoDetails Create(string? caption, DateOnly? takenOn, DateOnly today)
    {
        var text = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (text is not null && (text.Length > CaptionMaxLength || text.Any(char.IsControl)))
            throw new DomainRuleException("photo.caption_invalid");
        if (takenOn is { } day && day > today)
            throw new DomainRuleException("photo.date_invalid");
        return new GraveSitePhotoDetails(text, takenOn);
    }
}

/// <summary>
/// A photo of a grave site. The bytes live in file storage under two keys (the full-size and the
/// thumbnail JPEG); the photo itself only keeps the keys, so storage can move without touching it.
/// Photos are private to the organization unless <see cref="ShowOnMemorial"/> is set.
/// </summary>
public sealed class GraveSitePhoto : TenantEntity
{
    private GraveSitePhoto()
    {
        FullFileId = "";
        ThumbFileId = "";
    }

    public Guid GraveSiteId { get; private set; }

    public string FullFileId { get; private set; }

    public string ThumbFileId { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public string? Caption { get; private set; }

    public DateOnly? TakenOn { get; private set; }

    public bool IsCover { get; private set; }

    public bool ShowOnMemorial { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static GraveSitePhoto Take(Guid tenantId, Guid graveSiteId, GraveSitePhotoFiles files, GraveSitePhotoDetails details, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || graveSiteId == Guid.Empty)
            throw new DomainRuleException("grave_site.not_found");

        return new GraveSitePhoto
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            GraveSiteId = graveSiteId,
            FullFileId = files.FullFileId,
            ThumbFileId = files.ThumbFileId,
            Width = files.Width,
            Height = files.Height,
            Caption = details.Caption,
            TakenOn = details.TakenOn,
            CreatedAt = now,
        };
    }

    public void Describe(GraveSitePhotoDetails details)
    {
        Caption = details.Caption;
        TakenOn = details.TakenOn;
    }

    public void ShowOnMemorialPage() => ShowOnMemorial = true;

    public void HideFromMemorialPage() => ShowOnMemorial = false;

    public void MarkCover() => IsCover = true;

    public void ClearCover() => IsCover = false;

    /// <summary>Makes <paramref name="cover"/> the only cover among <paramref name="photos"/> of one grave site.</summary>
    public static void ChooseCover(IEnumerable<GraveSitePhoto> photos, GraveSitePhoto cover)
    {
        foreach (var photo in photos)
        {
            if (photo.GraveSiteId != cover.GraveSiteId)
                throw new DomainRuleException("photo.not_found");
            photo.ClearCover();
        }

        cover.MarkCover();
    }
}

/// <summary>The stored image files for one photo and the pixel size of the full-size image.</summary>
public sealed record GraveSitePhotoFiles
{
    public GraveSitePhotoFiles(string fullFileId, string thumbFileId, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(fullFileId) || string.IsNullOrWhiteSpace(thumbFileId) || width < 1 || height < 1)
            throw new DomainRuleException("photo.type_invalid");
        FullFileId = fullFileId;
        ThumbFileId = thumbFileId;
        Width = width;
        Height = height;
    }

    public string FullFileId { get; }

    public string ThumbFileId { get; }

    public int Width { get; }

    public int Height { get; }
}
