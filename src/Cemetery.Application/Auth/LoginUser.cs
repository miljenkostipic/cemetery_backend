using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Auth;

public sealed record LoginUser(string Email, string Password);

public sealed class LoginUserValidator : AbstractValidator<LoginUser>
{
    public LoginUserValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithErrorCode("auth.email_invalid");
        RuleFor(command => command.Password)
            .NotEmpty()
            .WithErrorCode("auth.password_invalid");
    }
}

public sealed class LoginUserHandler(
    IPasswordSignIn passwords,
    IUserAccountGateway users) : ICommandHandler<LoginUser, UserProfile>
{
    public async Task<UserProfile> Handle(LoginUser command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Parse(command.Email);
        var status = await passwords.SignInAsync(email, command.Password, cancellationToken).ConfigureAwait(false);
        if (status != PasswordSignInStatus.Succeeded)
            throw new UnauthorizedException(status == PasswordSignInStatus.LockedOut ? "auth.locked_out" : "auth.failed");

        var account = await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedException("auth.failed");
        return new UserProfile(account.Id, account.Email.Value, account.DisplayName);
    }
}
