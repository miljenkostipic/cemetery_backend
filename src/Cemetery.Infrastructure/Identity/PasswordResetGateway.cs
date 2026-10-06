using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Cemetery.Infrastructure.Identity;

internal sealed class PasswordResetGateway(UserManager<ApplicationUser> users) : IPasswordReset
{
    public async Task<string?> CreateTokenAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Value).ConfigureAwait(false);
        if (user is null)
            return null;

        return await users.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
    }

    public async Task<PasswordResetStatus> ResetAsync(
        EmailAddress email,
        string token,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Value).ConfigureAwait(false);
        if (user is null)
            return PasswordResetStatus.Failed;

        var result = await users.ResetPasswordAsync(user, token, password).ConfigureAwait(false);
        if (!result.Succeeded)
            return PasswordResetStatus.Failed;

        await ClearLockoutAsync(user).ConfigureAwait(false);
        return PasswordResetStatus.Succeeded;
    }

    private async Task ClearLockoutAsync(ApplicationUser user)
    {
        await users.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
    }
}
