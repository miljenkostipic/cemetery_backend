using System.Net;
using System.Net.Http.Json;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

public sealed class AuthEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Register_login_logout_and_session_round_trip()
    {
        var email = Email();
        var registered = Api.CreateClient();
        var created = await OfficeWorld.Post(registered, "/api/v1/auth/register", Account(email, "Ada"), CancellationToken);
        Assert.Equal(email, created.GetProperty("email").GetString());

        var session = await OfficeWorld.Get(registered, "/api/v1/auth/me", CancellationToken);
        Assert.Equal(email, session.GetProperty("email").GetString());

        var loggedOut = await registered.PostAsync("/api/v1/auth/logout", null, CancellationToken);
        await ApiAssert.Ok(loggedOut);
        Assert.Equal(HttpStatusCode.Unauthorized, (await registered.GetAsync("/api/v1/auth/me", CancellationToken)).StatusCode);

        var returning = Api.CreateClient();
        var signedIn = await returning.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Correct-Horse-1" }, CancellationToken);
        await ApiAssert.Ok(signedIn);
        Assert.Equal(email, (await OfficeWorld.Get(returning, "/api/v1/auth/me", CancellationToken)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Password_reset_replaces_the_password()
    {
        var email = Email();
        var client = Api.CreateClient();
        await OfficeWorld.Post(client, "/api/v1/auth/register", Account(email, "Ada"), CancellationToken);
        var requested = await client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, CancellationToken);
        await ApiAssert.Ok(requested);
        var token = Api.TakeReset(email);
        var confirmed = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/password-resets/confirm", new
        {
            email,
            token,
            password = "Correct-Horse-2",
        }, CancellationToken);
        await ApiAssert.Ok(confirmed);
        var signedIn = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Correct-Horse-2" }, CancellationToken);
        await ApiAssert.Ok(signedIn);
    }

    [Fact]
    public async Task Unknown_password_reset_stays_quiet()
    {
        var response = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/password-resets", new { email = Email() }, CancellationToken);
        await ApiAssert.Ok(response);
    }

    [Fact]
    public async Task Passkey_options_are_offered_and_a_bad_credential_is_refused()
    {
        var client = Api.CreateClient();
        await OfficeWorld.Post(client, "/api/v1/auth/register", Account(Email(), "Ada"), CancellationToken);
        var created = await client.PostAsync("/api/v1/auth/passkeys/creation-options", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await created.Content.ReadAsStringAsync(CancellationToken)));

        var requested = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/passkeys/request-options", new { email = Email() }, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var anonymous = await Api.CreateClient().PostAsync("/api/v1/auth/passkeys/request-options", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);

        var register = await client.PostAsJsonAsync("/api/v1/auth/passkeys", new { credentialJson = "{}" }, CancellationToken);
        await ApiAssert.Problem(register, HttpStatusCode.Unauthorized, "auth.passkey_failed");
        var login = await Api.CreateClient().PostAsJsonAsync("/api/v1/auth/passkeys/login", new { credentialJson = "{}" }, CancellationToken);
        await ApiAssert.Problem(login, HttpStatusCode.Unauthorized, "auth.passkey_failed");
    }

    [Fact]
    public async Task Health_is_public()
    {
        var response = await Api.CreateClient().GetAsync("/api/v1/health", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_session_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().GetAsync("/api/v1/auth/me", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().PostAsync("/api/v1/auth/logout", null, CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/register", "auth.email_invalid")]
    [InlineData("/api/v1/auth/login", "auth.email_invalid")]
    [InlineData("/api/v1/auth/password-resets", "auth.email_invalid")]
    [InlineData("/api/v1/auth/password-resets/confirm", "auth.email_invalid")]
    public async Task Validation_is_400(string url, string code)
    {
        var response = await Api.CreateClient().PostAsJsonAsync(url, new { email = "not-an-email" }, CancellationToken);
        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task One_user_cannot_read_another_session()
    {
        var first = Email();
        var second = Email();
        var ada = Api.CreateClient();
        var ivan = Api.CreateClient();
        await OfficeWorld.Post(ada, "/api/v1/auth/register", Account(first, "Ada"), CancellationToken);
        await OfficeWorld.Post(ivan, "/api/v1/auth/register", Account(second, "Ivan"), CancellationToken);
        Assert.Equal(second, (await OfficeWorld.Get(ivan, "/api/v1/auth/me", CancellationToken)).GetProperty("email").GetString());
        Assert.NotEqual(first, (await OfficeWorld.Get(ivan, "/api/v1/auth/me", CancellationToken)).GetProperty("email").GetString());
    }

    private static string Email() => $"ada.{Guid.NewGuid():N}@example.com";

    private static object Account(string email, string name) => new { email, password = "Correct-Horse-1", displayName = name };
}
