namespace Cemetery.Domain.Identity;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    public static bool IsSatisfied(string? password)
    {
        if (password is null || password.Length < MinimumLength)
            return false;

        var hasLower = false;
        var hasUpper = false;
        var hasDigit = false;
        foreach (var character in password)
        {
            hasLower |= char.IsLower(character);
            hasUpper |= char.IsUpper(character);
            hasDigit |= char.IsDigit(character);
        }

        return hasLower && hasUpper && hasDigit;
    }
}
