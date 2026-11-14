using Microsoft.Extensions.Configuration;

namespace Cemetery.Infrastructure.Storage;

/// <summary>A Cloudflare R2 account and the private bucket that holds tenant files.</summary>
internal sealed record R2Account(string AccountId, string AccessKey, string SecretKey, string BucketName)
{
    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}

/// <summary>
/// Reads the <c>R2</c> section, the same keys Matica uses (<c>R2:AccountId</c>, <c>R2:AccessKey</c>,
/// <c>R2:SecretKey</c>, <c>R2:BucketName</c>). No access key keeps files in the database; a key with
/// any other part missing is a misconfiguration and refuses to start.
/// </summary>
internal static class R2Settings
{
    public const string Section = "R2";

    public static R2Account? Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(Section);
        var accessKey = section["AccessKey"];
        if (string.IsNullOrWhiteSpace(accessKey))
            return null;

        var accountId = section["AccountId"];
        var secretKey = section["SecretKey"];
        var bucketName = section["BucketName"];
        if (!IsAccountId(accountId) || string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("R2:AccountId, R2:SecretKey, and R2:BucketName must identify an R2 bucket when R2:AccessKey is set.");

        return new R2Account(accountId.Trim(), accessKey.Trim(), secretKey.Trim(), bucketName.Trim());
    }

    private static bool IsAccountId([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? accountId) =>
        !string.IsNullOrWhiteSpace(accountId) && accountId.Trim().All(char.IsAsciiLetterOrDigit);
}
