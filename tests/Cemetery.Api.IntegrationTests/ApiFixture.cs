using System.Net.Http.Json;
using System.Text.Json;
using Cemetery.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Cemetery.Api.IntegrationTests;

public sealed partial class ApiFixture : IAsyncLifetime
{
    public static int ContainersStarted { get; private set; }

    public static bool UsesLocalServer { get; private set; }

    private readonly InvitationInbox inbox = new();
    private readonly List<HttpClient> clients = [];
    private readonly Dictionary<string, HttpClient> admins = [];
    private PostgreSqlContainer? container;
    private WebApplicationFactory<Program>? factory;
    private Respawner? respawner;
    private string? ownerConnection;
    private string? appConnection;
    private int userCount;
    private int cemeteryCount;
    private int sectionCount;
    private int deceasedCount;

    public bool IsReady => factory is not null;

    public string OwnerConnection =>
        ownerConnection ?? throw new InvalidOperationException("Docker is not available.");

    public async ValueTask InitializeAsync()
    {
        if (DockerProbe.EnsureAvailable())
        {
            await StartContainerAsync().ConfigureAwait(false);
            return;
        }

        await StartLocalServerAsync().ConfigureAwait(false);
    }

    private async Task StartContainerAsync()
    {
        container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
            .WithDatabase("cemetery")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await container.StartAsync();
        ContainersStarted++;
        ownerConnection = container.GetConnectionString();
        appConnection = AppConnection(ownerConnection);
        await PrepareAsync().ConfigureAwait(false);
        Console.WriteLine($"ApiFixture started one PostGIS container ({container.Name}). ContainersStarted={ContainersStarted}.");
    }

    private async Task StartLocalServerAsync()
    {
        const string database = "cemetery_test";
        var owner = LocalConnection("ConnectionStrings:Owner", database);
        await EnsureLocalDatabaseAsync(owner, database).ConfigureAwait(false);
        ownerConnection = owner.ConnectionString;
        appConnection = LocalConnection("ConnectionStrings:App", database).ConnectionString;
        UsesLocalServer = true;
        await PrepareAsync().ConfigureAwait(false);
        Console.WriteLine($"ApiFixture is using local PostgreSQL database {database}.");
    }

    private async Task PrepareAsync()
    {
        await DatabaseInitializer.ApplyAsync(ownerConnection!, appConnection!, CancellationToken.None).ConfigureAwait(false);
        respawner = await CreateRespawnerAsync(ownerConnection!);
        factory = CreateFactory();
    }

    private static NpgsqlConnectionStringBuilder LocalConnection(string key, string database)
    {
        var configured = ReadSecret(key)
            ?? "Host=localhost;Port=5432;Database=cemetery;Username=postgres;Password=postgres";
        return new NpgsqlConnectionStringBuilder(configured) { Database = database };
    }

    private static string? ReadSecret(string key)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            "a082eb8d-4c2d-45ca-aa0d-3cc997c18bc1",
            "secrets.json");
        if (!File.Exists(path))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.TryGetProperty(key, out var flat))
            return flat.GetString();

        var parts = key.Split(':');
        var node = document.RootElement;
        foreach (var part in parts)
        {
            if (!node.TryGetProperty(part, out node))
                return null;
        }

        return node.GetString();
    }

    private static async Task EnsureLocalDatabaseAsync(NpgsqlConnectionStringBuilder owner, string database)
    {
        var admin = new NpgsqlConnectionStringBuilder(owner.ConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        exists.Parameters.AddWithValue("name", database);
        if (await exists.ExecuteScalarAsync().ConfigureAwait(false) is not null)
            return;

        await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await create.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        DisposeClients();
        if (factory is not null)
            await factory.DisposeAsync();
        if (container is not null)
            await container.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    public async Task ResetAsync()
    {
        DisposeClients();
        admins.Clear();
        inbox.Clear();
        userCount = 0;
        cemeteryCount = 0;
        sectionCount = 0;
        deceasedCount = 0;
        await ResetDatabaseAsync();
    }

    public string TakeInvitation(string email) => inbox.TakeInvitation(email);

    public string TakeReset(string email) => inbox.TakeReset(email);

    public HttpClient CreateClient()
    {
        var client = Factory().CreateClient();
        clients.Add(client);
        return client;
    }

    private WebApplicationFactory<Program> Factory() =>
        factory ?? throw new InvalidOperationException("Docker is not available.");

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Owner", ownerConnection);
            builder.UseSetting("ConnectionStrings:App", appConnection);
            builder.UseSetting("App:PublicUrl", "http://localhost:5173");
            builder.ConfigureTestServices(ReplaceEmail);
        });

    private void ReplaceEmail(IServiceCollection services)
    {
        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(Cemetery.Application.Abstractions.IEmailSender)).ToList();
        foreach (var descriptor in existing)
            services.Remove(descriptor);

        services.AddSingleton<Cemetery.Application.Abstractions.IEmailSender>(inbox);
    }

    private static string AppConnection(string owner) =>
        new NpgsqlConnectionStringBuilder(owner) { Username = "cemetery_app", Password = "cemetery_app" }.ConnectionString;

    private static async Task<Respawner> CreateRespawnerAsync(string owner)
    {
        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        // Respawn uses SchemasToExclude in place of SchemasToInclude when both are set.
        // Resetting only the cemetery schema leaves the Hangfire schema untouched.
        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["cemetery"],
            TablesToIgnore =
            [
                new Table("cemetery", "__ef_migrations"),
                new Table("__EFMigrationsHistory"),
            ],
        });
    }

    private async Task ResetDatabaseAsync()
    {
        if (respawner is null || ownerConnection is null)
            throw new InvalidOperationException("Docker is not available.");

        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync();
        await respawner.ResetAsync(connection);
    }

    private void DisposeClients()
    {
        foreach (var client in clients)
            client.Dispose();

        clients.Clear();
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureOkAsync(response);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ReadFirstIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureOkAsync(response);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return ApiAssert.Rows(body).EnumerateArray().First().GetProperty("id").GetGuid();
    }
}
