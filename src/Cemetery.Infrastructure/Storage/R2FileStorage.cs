using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Cemetery.Application.Abstractions;
using Cemetery.Infrastructure.Persistence;

namespace Cemetery.Infrastructure.Storage;

/// <summary>
/// Keeps tenant files in a private Cloudflare R2 bucket under <c>{tenantId}/{uuid v7}</c>. A key outside the
/// current tenant's prefix reads as missing. Keys that are bare UUIDs predate R2 and still live in the
/// database, so they go to <see cref="DatabaseFileStorage"/>.
/// </summary>
internal sealed class R2FileStorage(IAmazonS3 s3, R2Account account, ITenantContext tenant, DatabaseFileStorage legacy) : IFileStorage
{
    private const string ContentType = "application/octet-stream";

    public async Task<string> PutAsync(Guid tenantId, byte[] content, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || content.Length == 0)
            throw new Domain.Identity.DomainRuleException("document.empty");

        var key = Prefix(tenantId) + Guid.CreateVersion7().ToString("D");
        using var body = new MemoryStream(content, writable: false);
        var put = new PutObjectRequest
        {
            BucketName = account.BucketName,
            Key = key,
            InputStream = body,
            ContentType = ContentType,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };
        put.Headers.ContentLength = content.Length;
        await s3.PutObjectAsync(put, cancellationToken).ConfigureAwait(false);
        return key;
    }

    public async Task<byte[]?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        if (IsLegacy(key))
            return await legacy.OpenAsync(key, cancellationToken).ConfigureAwait(false);
        if (!Owned(key))
            return null;

        try
        {
            using var response = await s3.GetObjectAsync(account.BucketName, key, cancellationToken).ConfigureAwait(false);
            return await ReadAll(response, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (IsLegacy(key))
        {
            await legacy.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (Owned(key))
            await s3.DeleteObjectAsync(account.BucketName, key, cancellationToken).ConfigureAwait(false);
    }

    internal static string Prefix(Guid tenantId) => tenantId.ToString("D") + "/";

    private static bool IsLegacy(string key) => Guid.TryParseExact(key, "D", out _);

    private bool Owned(string key)
    {
        var current = tenant.OrganizationId;
        if (current is null || current == Guid.Empty || string.IsNullOrWhiteSpace(key))
            return false;

        var prefix = Prefix(current.Value);
        return key.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(key[prefix.Length..], "D", out _);
    }

    private static async Task<byte[]> ReadAll(GetObjectResponse response, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    public static IAmazonS3 Client(R2Account account) =>
        new AmazonS3Client(
            new BasicAWSCredentials(account.AccessKey, account.SecretKey),
            new AmazonS3Config { ServiceURL = account.ServiceUrl, ForcePathStyle = true });
}
