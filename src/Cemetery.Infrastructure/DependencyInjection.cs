using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Identity;
using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cemetery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool headless)
    {
        services.AddScoped<AmbientTenant>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<AmbientTenant>());
        services.AddScoped<AmbientSession>();
        services.AddScoped<ISessionHints>(provider => provider.GetRequiredService<AmbientSession>());
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<CemeteryDbContext>((provider, options) => ConfigureDb(provider, options, configuration));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CemeteryDbContext>());
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<ILayoutRepository, LayoutRepository>();
        services.AddScoped<IUserAccountGateway, UserAccountGateway>();
        services.AddScoped<IPasswordSignIn, PasswordSignInGateway>();
        services.AddSingleton<IInvitationTokenFactory, InvitationTokenFactory>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<IAppLinks, ConfiguredAppLinks>();
        AddIdentity(services);
        if (!headless)
            AddHangfire(services, configuration);
        return services;
    }

    private static void ConfigureDb(IServiceProvider provider, DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException("ConnectionStrings:App is required.");
        options.UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite().MigrationsHistoryTable("__ef_migrations", "cemetery"));
        options.UseSnakeCaseNamingConvention();
        options.AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>());
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.Password.RequiredLength = PasswordPolicy.MinimumLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = PasswordPolicy.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = PasswordPolicy.Lockout;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<CemeteryDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "cemetery.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    private static void AddHangfire(IServiceCollection services, IConfiguration configuration)
    {
        var owner = configuration.GetConnectionString("Owner")
            ?? throw new InvalidOperationException("ConnectionStrings:Owner is required.");
        services.AddHangfire(config => config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(owner)));
        services.AddHangfireServer();
    }
}
