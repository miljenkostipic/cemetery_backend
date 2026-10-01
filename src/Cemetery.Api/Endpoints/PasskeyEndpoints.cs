using Cemetery.Application.Exceptions;
using Cemetery.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Cemetery.Api.Endpoints;

public static class PasskeyEndpoints
{
    public sealed record PasskeyCredential(string CredentialJson);

    public static RouteGroupBuilder MapPasskeys(this RouteGroupBuilder auth)
    {
        auth.MapPost("/passkeys", Register).RequireAuthorization();
        var passkeys = auth.MapGroup("/passkeys");
        passkeys.MapPost("/creation-options", CreationOptions).RequireAuthorization();
        passkeys.MapPost("/request-options", RequestOptions).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        passkeys.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(AuthRateLimit.Policy);
        return auth;
    }

    private static async Task<IResult> CreationOptions(
        HttpContext http,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        var user = await users.GetUserAsync(http.User).ConfigureAwait(false);
        if (user is null)
            return Results.Unauthorized();

        var json = await signIn.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.Email ?? user.UserName ?? "user",
            DisplayName = user.DisplayName,
        }).ConfigureAwait(false);
        return Results.Content(json, "application/json");
    }

    private static async Task<NoContent> Register(
        PasskeyCredential body,
        HttpContext http,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        var user = await users.GetUserAsync(http.User).ConfigureAwait(false);
        if (user is null)
            throw new UnauthorizedException("auth.required");

        var attestation = await signIn.PerformPasskeyAttestationAsync(body.CredentialJson).ConfigureAwait(false);
        if (!attestation.Succeeded)
            throw new UnauthorizedException("auth.passkey_failed");

        var stored = await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey).ConfigureAwait(false);
        if (!stored.Succeeded)
            throw new ConflictException("auth.passkey_failed");

        return TypedResults.NoContent();
    }

    private static async Task<IResult> RequestOptions(
        string? email,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        ApplicationUser? user = string.IsNullOrWhiteSpace(email) ? null : await users.FindByEmailAsync(email).ConfigureAwait(false);
        var json = await signIn.MakePasskeyRequestOptionsAsync(user).ConfigureAwait(false);
        return Results.Content(json, "application/json");
    }

    private static async Task<NoContent> Login(PasskeyCredential body, SignInManager<ApplicationUser> signIn)
    {
        var result = await signIn.PasskeySignInAsync(body.CredentialJson).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new UnauthorizedException("auth.passkey_failed");

        return TypedResults.NoContent();
    }
}
