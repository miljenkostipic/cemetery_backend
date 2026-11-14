using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Cemetery.Infrastructure.Storage;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cemetery.Architecture.Tests;

public sealed class R2FileStorageTests
{
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();
    private static readonly R2Account Account = new("abc123", "access", "secret", "cemetery-files");

    [Fact]
    public void Missing_access_key_keeps_files_in_the_database()
    {
        Assert.Null(R2Settings.Read(Config(new() { ["R2:BucketName"] = "cemetery-files" })));
    }

    [Fact]
    public void Complete_settings_point_at_the_account_endpoint()
    {
        var account = R2Settings.Read(Config(Settings("abc123")));

        Assert.NotNull(account);
        Assert.Equal("https://abc123.r2.cloudflarestorage.com", account.ServiceUrl);
        Assert.Equal("cemetery-files", account.BucketName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("evil.example.com/x")]
    public void Access_key_without_a_valid_account_refuses_to_start(string accountId)
    {
        Assert.Throws<InvalidOperationException>(() => R2Settings.Read(Config(Settings(accountId))));
    }

    [Fact]
    public async Task Put_writes_under_the_tenant_prefix()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var storage = Storage(s3, TenantA);

        var key = await storage.PutAsync(TenantA, [1, 2, 3], CancellationToken.None);

        Assert.StartsWith(TenantA + "/", key, StringComparison.Ordinal);
        await s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(put => put.Key == key && put.BucketName == "cemetery-files" && put.DisablePayloadSigning == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Open_returns_the_object_bytes()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var key = R2FileStorage.Prefix(TenantA) + Guid.CreateVersion7();
        s3.GetObjectAsync("cemetery-files", key, Arg.Any<CancellationToken>())
            .Returns(_ => new GetObjectResponse { ResponseStream = new MemoryStream([7, 8]) });

        var content = await Storage(s3, TenantA).OpenAsync(key, CancellationToken.None);

        Assert.Equal([7, 8], content);
    }

    [Fact]
    public async Task Another_tenants_key_reads_as_missing_and_is_never_fetched_or_deleted()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var key = R2FileStorage.Prefix(TenantA) + Guid.CreateVersion7();
        var storage = Storage(s3, TenantB);

        Assert.Null(await storage.OpenAsync(key, CancellationToken.None));
        await storage.DeleteAsync(key, CancellationToken.None);

        await s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default!, TestContext.Current.CancellationToken);
        await s3.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("")]
    public async Task Malformed_keys_read_as_missing(string suffix)
    {
        var s3 = Substitute.For<IAmazonS3>();

        Assert.Null(await Storage(s3, TenantA).OpenAsync(R2FileStorage.Prefix(TenantA) + suffix, CancellationToken.None));
        await s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Missing_object_reads_as_missing()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var key = R2FileStorage.Prefix(TenantA) + Guid.CreateVersion7();
        s3.GetObjectAsync("cemetery-files", key, Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });

        Assert.Null(await Storage(s3, TenantA).OpenAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_an_owned_object()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var key = R2FileStorage.Prefix(TenantA) + Guid.CreateVersion7();

        await Storage(s3, TenantA).DeleteAsync(key, CancellationToken.None);

        await s3.Received(1).DeleteObjectAsync("cemetery-files", key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_content_is_refused_before_upload()
    {
        var s3 = Substitute.For<IAmazonS3>();

        await Assert.ThrowsAsync<Cemetery.Domain.Identity.DomainRuleException>(() => Storage(s3, TenantA).PutAsync(TenantA, [], CancellationToken.None));
        await s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!, TestContext.Current.CancellationToken);
    }

    private static R2FileStorage Storage(IAmazonS3 s3, Guid tenantId)
    {
        var tenant = new AmbientTenant();
        tenant.Use(tenantId);
        return new R2FileStorage(s3, Account, tenant, null!);
    }

    private static Dictionary<string, string?> Settings(string accountId) => new()
    {
        ["R2:AccountId"] = accountId,
        ["R2:AccessKey"] = "access",
        ["R2:SecretKey"] = "secret",
        ["R2:BucketName"] = "cemetery-files",
    };

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
