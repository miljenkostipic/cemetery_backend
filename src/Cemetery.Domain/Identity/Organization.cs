using Cemetery.Domain.Register;

namespace Cemetery.Domain.Identity;

public sealed class Organization
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 200;

    private Organization()
    {
        Name = "";
        Slug = "";
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public bool PublishesRegister { get; private set; }

    public int HideRecentDeathsDays { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return trimmed.Length >= NameMinLength && trimmed.Length <= NameMaxLength;
    }

    public static Organization Create(string name, string slug, DateTimeOffset createdAt)
    {
        if (!IsValidName(name))
            throw new DomainRuleException("organization.name_invalid");

        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainRuleException("organization.slug_empty");

        return new Organization
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Slug = slug,
            CreatedAt = createdAt,
        };
    }

    public void PublishRegister(int hideRecentDeathsDays)
    {
        HideRecentDeathsDays = PublicListing.RequireHideDays(hideRecentDeathsDays);
        PublishesRegister = true;
    }

    public void WithdrawRegister() => PublishesRegister = false;
}
