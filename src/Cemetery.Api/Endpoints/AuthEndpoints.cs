using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Cemetery.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");
        auth.MapPost("/register", Register).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        auth.MapPost("/password-resets", RequestReset).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        auth.MapPost("/password-resets/confirm", ConfirmReset).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        auth.MapPost("/logout", Logout).RequireAuthorization();
        auth.MapGet("/me", Me).RequireAuthorization();
        auth.MapPasskeys();
        return api;
    }

    private static async Task<Ok<UserProfile>> Register(
        RegisterUser command,
        ICommandHandler<RegisterUser, UserProfile> handler,
        CancellationToken cancellationToken)
    {
        var profile = await handler.Handle(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(profile);
    }

    private static async Task<Ok<UserProfile>> Login(
        LoginUser command,
        ICommandHandler<LoginUser, UserProfile> handler,
        CancellationToken cancellationToken)
    {
        var profile = await handler.Handle(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(profile);
    }

    private static async Task<NoContent> RequestReset(
        RequestPasswordReset command,
        ICommandHandler<RequestPasswordReset, PasswordResetDelivery> handler,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var delivery = await handler.Handle(command, cancellationToken).ConfigureAwait(false);
        LogReset(loggerFactory.CreateLogger("Cemetery.Mail"), command.Email, delivery);
        return TypedResults.NoContent();
    }

    private static void LogReset(ILogger logger, string email, PasswordResetDelivery delivery)
    {
        if (!logger.IsEnabled(LogLevel.Information) || delivery != PasswordResetDelivery.Skipped)
            return;

        logger.LogInformation("Password reset not sent. No account for {Email}.", email);
    }

    private static async Task<Ok<UserProfile>> ConfirmReset(
        ConfirmPasswordReset command,
        ICommandHandler<ConfirmPasswordReset, UserProfile> handler,
        CancellationToken cancellationToken)
    {
        var profile = await handler.Handle(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(profile);
    }

    private static async Task<NoContent> Logout(SignInManager<ApplicationUser> signIn, HttpContext http)
    {
        await signIn.SignOutAsync().ConfigureAwait(false);
        http.Response.Cookies.Delete(Tenancy.CookieOrganizationStore.Name);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<SessionView>> Me(
        IQueryHandler<GetSession, SessionView> handler,
        CancellationToken cancellationToken)
    {
        var session = await handler.Handle(new GetSession(), cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(session);
    }
}

public static class AuthRateLimit
{
    public const string Policy = "auth";
    public const int PermitLimit = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
