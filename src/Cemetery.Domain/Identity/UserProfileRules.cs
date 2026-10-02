namespace Cemetery.Domain.Identity;

public static class UserProfileRules
{
    public const int DisplayNameMaxLength = 100;

    public static bool IsValidDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Trim().Length <= DisplayNameMaxLength;
    }
}
