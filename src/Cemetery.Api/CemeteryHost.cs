using System.Threading.RateLimiting;
using Cemetery.Api.Endpoints;
using Cemetery.Api.Errors;
using Cemetery.Api.Tenancy;
using Cemetery.Application;
using Cemetery.Application.Abstractions;
using Cemetery.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

namespace Cemetery.Api;

public static class CemeteryHost
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var headless = args.Contains("--export-openapi") || builder.Environment.IsEnvironment("Testing");
        ConfigureServices(builder, headless);
        var app = builder.Build();
        ConfigurePipeline(app);
        return app;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var configuration = app.Configuration;
        var owner = configuration.GetConnectionString("Owner") ?? throw new InvalidOperationException("ConnectionStrings:Owner is required.");
        var application = configuration.GetConnectionString("App") ?? throw new InvalidOperationException("ConnectionStrings:App is required.");
        await DatabaseInitializer.ApplyAsync(owner, application, CancellationToken.None).ConfigureAwait(false);
    }

    private static void ConfigureServices(WebApplicationBuilder builder, bool headless)
    {
        builder.Host.UseSerilog((_, configuration) =>
            configuration.MinimumLevel.Information().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration, headless);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddScoped<IActiveOrganizationStore, CookieOrganizationStore>();
        builder.Services.AddExceptionHandler<CemeteryExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Servers = [new() { Url = "http://localhost:5080" }];
                return Task.CompletedTask;
            });
        });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("tenant", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRequirement()));
        });
        builder.Services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimit.Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthRateLimit.PermitLimit,
                    Window = AuthRateLimit.Window,
                    QueueLimit = 0,
                }));
        });
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy("spa", policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.UseCors("spa");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthorization();
        app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
        if (app.Environment.IsDevelopment())
            ConfigureApiDocUis(app);

        app.MapGroup("/api/v1").MapAuthEndpoints().MapOrganizationEndpoints().MapLayoutEndpoints();
    }

    private static void ConfigureApiDocUis(WebApplication app)
    {
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Cemetery API v1");
            options.RoutePrefix = "swagger";
        });
        app.MapScalarApiReference().AllowAnonymous();
    }
}

public static class OpenApiExporter
{
    public static async Task ExportAsync(WebApplication app)
    {
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync().ConfigureAwait(false);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First(url => url.StartsWith("http://", StringComparison.Ordinal))) };
        var json = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)).ConfigureAwait(false);
        var directory = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "openapi"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "cemetery.json"), json).ConfigureAwait(false);
        await app.StopAsync().ConfigureAwait(false);
    }
}
