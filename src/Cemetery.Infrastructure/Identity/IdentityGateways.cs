using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Cemetery.Infrastructure.Identity;

internal sealed class UserAccountGateway(UserManager<ApplicationUser> users) : IUserAccountGateway
{
    public async Task<Guid> CreateAsync(EmailAddress email, string password, string displayName, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email.Value,
            Email = email.Value,
            DisplayName = displayName,
            EmailConfirmed = true,
        };
        var result = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (result.Succeeded)
            return user.Id;

        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw new ConflictException("auth.email_taken");

        throw new ValidationFailedException(["auth.password_invalid"]);
    }

    public async Task<UserAccount?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Value).ConfigureAwait(false);
        if (user?.Email is null)
            return null;

        return new UserAccount(user.Id, EmailAddress.Parse(user.Email), user.DisplayName);
    }
}

internal sealed class PasswordSignInGateway(SignInManager<ApplicationUser> signIn) : IPasswordSignIn
{
    public async Task<PasswordSignInStatus> SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken)
    {
        var result = await signIn.PasswordSignInAsync(email.Value, password, isPersistent: true, lockoutOnFailure: true)
            .ConfigureAwait(false);
        if (result.Succeeded)
            return PasswordSignInStatus.Succeeded;

        return result.IsLockedOut ? PasswordSignInStatus.LockedOut : PasswordSignInStatus.Failed;
    }
}
