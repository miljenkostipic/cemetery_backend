using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using FluentValidation;

namespace Cemetery.Application.Auth;

public enum PasswordResetDelivery
{
    Skipped,
    Sent,
}

public sealed record RequestPasswordReset(string Email);

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordReset>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithErrorCode("auth.email_invalid");
    }
}

public sealed class RequestPasswordResetHandler(IPasswordReset resets, IEmailSender email, IAppLinks links)
    : ICommandHandler<RequestPasswordReset, PasswordResetDelivery>
{
    public async Task<PasswordResetDelivery> Handle(RequestPasswordReset command, CancellationToken cancellationToken)
    {
        var address = EmailAddress.Parse(command.Email);
        var token = await resets.CreateTokenAsync(address, cancellationToken).ConfigureAwait(false);
        if (token is null)
            return PasswordResetDelivery.Skipped;

        await email.SendPasswordResetAsync(address, links.PasswordResetUrl(address.Value, token), cancellationToken)
            .ConfigureAwait(false);
        return PasswordResetDelivery.Sent;
    }
}

public sealed record ConfirmPasswordReset(string Email, string Token, string Password);

public sealed class ConfirmPasswordResetValidator : AbstractValidator<ConfirmPasswordReset>
{
    public ConfirmPasswordResetValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithErrorCode("auth.email_invalid");
        RuleFor(command => command.Token)
            .NotEmpty()
            .WithErrorCode("auth.reset_invalid");
        RuleFor(command => command.Password)
            .Must(PasswordPolicy.IsSatisfied)
            .WithErrorCode("auth.password_invalid");
    }
}

public sealed class ConfirmPasswordResetHandler(
    IPasswordReset resets,
    IPasswordSignIn passwords,
    IUserAccountGateway users) : ICommandHandler<ConfirmPasswordReset, UserProfile>
{
    public async Task<UserProfile> Handle(ConfirmPasswordReset command, CancellationToken cancellationToken)
    {
        var address = EmailAddress.Parse(command.Email);
        var status = await resets.ResetAsync(address, command.Token, command.Password, cancellationToken).ConfigureAwait(false);
        if (status != PasswordResetStatus.Succeeded)
            throw new UnauthorizedException("auth.reset_invalid");

        await SignInAsync(address, command.Password, cancellationToken).ConfigureAwait(false);
        var account = await users.FindByEmailAsync(address, cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedException("auth.reset_invalid");
        return new UserProfile(account.Id, account.Email.Value, account.DisplayName);
    }

    private async Task SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken)
    {
        var status = await passwords.SignInAsync(email, password, cancellationToken).ConfigureAwait(false);
        if (status == PasswordSignInStatus.LockedOut)
            throw new UnauthorizedException("auth.locked_out");
        if (status != PasswordSignInStatus.Succeeded)
            throw new UnauthorizedException("auth.reset_invalid");
    }
}
