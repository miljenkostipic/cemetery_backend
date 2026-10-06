using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class PasswordResetHandlerTests
{
    private static readonly EmailAddress Address = EmailAddress.Parse("ada@example.com");

    [Fact]
    public async Task Request_does_not_send_a_link_when_the_account_is_missing()
    {
        var resets = Substitute.For<IPasswordReset>();
        resets.CreateTokenAsync(Address, Arg.Any<CancellationToken>()).Returns((string?)null);
        var email = Substitute.For<IEmailSender>();
        var handler = new RequestPasswordResetHandler(resets, email, Substitute.For<IAppLinks>());

        var delivery = await handler.Handle(new RequestPasswordReset(Address.Value), CancellationToken.None);

        Assert.Equal(PasswordResetDelivery.Skipped, delivery);
        await email.DidNotReceive().SendPasswordResetAsync(Arg.Any<EmailAddress>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_sends_a_link_when_the_account_exists()
    {
        var resets = Substitute.For<IPasswordReset>();
        resets.CreateTokenAsync(Address, Arg.Any<CancellationToken>()).Returns("token");
        var links = Substitute.For<IAppLinks>();
        links.PasswordResetUrl(Address.Value, "token").Returns("http://localhost:5173/password-reset?email=ada%40example.com&token=token");
        var email = Substitute.For<IEmailSender>();
        var handler = new RequestPasswordResetHandler(resets, email, links);

        var delivery = await handler.Handle(new RequestPasswordReset("  Ada@Example.com "), CancellationToken.None);

        Assert.Equal(PasswordResetDelivery.Sent, delivery);

        await email.Received(1).SendPasswordResetAsync(
            Address,
            "http://localhost:5173/password-reset?email=ada%40example.com&token=token",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_rejects_an_invalid_token()
    {
        var resets = Substitute.For<IPasswordReset>();
        resets.ResetAsync(Address, "stale", "Correct-Horse-1", Arg.Any<CancellationToken>())
            .Returns(PasswordResetStatus.Failed);
        var passwords = Substitute.For<IPasswordSignIn>();
        var handler = new ConfirmPasswordResetHandler(resets, passwords, Substitute.For<IUserAccountGateway>());

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new ConfirmPasswordReset(Address.Value, "stale", "Correct-Horse-1"), CancellationToken.None));

        Assert.Equal("auth.reset_invalid", error.Code);
        await passwords.DidNotReceive().SignInAsync(Arg.Any<EmailAddress>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_signs_in_after_the_password_changes()
    {
        var userId = Guid.CreateVersion7();
        var resets = Substitute.For<IPasswordReset>();
        resets.ResetAsync(Address, "token", "Correct-Horse-1", Arg.Any<CancellationToken>())
            .Returns(PasswordResetStatus.Succeeded);
        var passwords = Substitute.For<IPasswordSignIn>();
        passwords.SignInAsync(Address, "Correct-Horse-1", Arg.Any<CancellationToken>())
            .Returns(PasswordSignInStatus.Succeeded);
        var users = Substitute.For<IUserAccountGateway>();
        users.FindByEmailAsync(Address, Arg.Any<CancellationToken>())
            .Returns(new UserAccount(userId, Address, "Ada"));
        var handler = new ConfirmPasswordResetHandler(resets, passwords, users);

        var profile = await handler.Handle(
            new ConfirmPasswordReset(Address.Value, "token", "Correct-Horse-1"),
            CancellationToken.None);

        Assert.Equal(userId, profile.UserId);
        Assert.Equal(Address.Value, profile.Email);
        Assert.Equal("Ada", profile.DisplayName);
    }
}
