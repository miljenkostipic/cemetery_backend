namespace Cemetery.Domain.Identity;

public readonly record struct EmailAddress
{
    public const int MaxLength = 256;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static bool TryParse(string? raw, out EmailAddress email)
    {
        email = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim().ToLowerInvariant();
        var at = value.IndexOf('@');
        if (value.Length > MaxLength || at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
            return false;

        email = new EmailAddress(value);
        return true;
    }

    public static EmailAddress Parse(string raw) =>
        TryParse(raw, out var email) ? email : throw new DomainRuleException("auth.email_invalid");
}
