using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Auth;

public sealed record RegisterUser(string Email, string Password, string DisplayName);

public sealed record UserProfile(Guid UserId, string Email, string DisplayName);

public sealed class RegisterUserValidator : AbstractValidator<RegisterUser>
{
    public RegisterUserValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithErrorCode("auth.email_invalid");
        RuleFor(command => command.Password)
            .Must(PasswordPolicy.IsSatisfied)
            .WithErrorCode("auth.password_invalid");
        RuleFor(command => command.DisplayName)
            .Must(UserProfileRules.IsValidDisplayName)
            .WithErrorCode("auth.display_name_invalid");
    }
}

public sealed class RegisterUserHandler(
    IUserAccountGateway users,
    IPasswordSignIn passwords) : ICommandHandler<RegisterUser, UserProfile>
{
    public async Task<UserProfile> Handle(RegisterUser command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Parse(command.Email);
        var displayName = command.DisplayName.Trim();
        var userId = await users.CreateAsync(email, command.Password, displayName, cancellationToken).ConfigureAwait(false);
        await SignInAsync(email, command.Password, cancellationToken).ConfigureAwait(false);
        return new UserProfile(userId, email.Value, displayName);
    }

    private async Task SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken)
    {
        var status = await passwords.SignInAsync(email, password, cancellationToken).ConfigureAwait(false);
        if (status != PasswordSignInStatus.Succeeded)
            throw new UnauthorizedException("auth.failed");
    }
}
