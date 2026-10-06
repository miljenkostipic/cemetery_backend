using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public readonly record struct PersonName
{
    public const int MaxLength = 80;

    public string Given { get; }

    public string Family { get; }

    private PersonName(string given, string family)
    {
        Given = given;
        Family = family;
    }

    public static PersonName Create(string? given, string? family)
    {
        var givenName = Normalize(given, "deceased.given_name_invalid");
        var familyName = Normalize(family, "deceased.family_name_invalid");
        return new PersonName(givenName, familyName);
    }

    private static string Normalize(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainRuleException(errorCode);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainRuleException(errorCode);

        return trimmed;
    }
}
