using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public readonly record struct ListingFacts(
    bool PublishesRegister,
    bool GraveHiddenFromPublic,
    DateOnly? DiedOn,
    int HideRecentDeathsDays,
    DateOnly Today);

public static class PublicListing
{
    public const int HideRecentDeathsMaxDays = 3650;
    public const int EpitaphMaxLength = 500;
    public const int PhotoUrlMaxLength = 2000;
    public const int SearchNameMinLength = 2;
    public const int SearchResultLimit = 50;

    public static bool IsListed(ListingFacts facts)
    {
        if (!facts.PublishesRegister || facts.GraveHiddenFromPublic)
            return false;
        if (facts.HideRecentDeathsDays <= 0)
            return true;
        return facts.DiedOn is DateOnly diedOn && Revealed(diedOn, facts.HideRecentDeathsDays, facts.Today);
    }

    public static int RequireHideDays(int days)
    {
        if (days < 0 || days > HideRecentDeathsMaxDays)
            throw new DomainRuleException("visibility.days_invalid");
        return days;
    }

    public static string RequireSearchName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length < SearchNameMinLength || trimmed.Length > PersonName.MaxLength || !LettersOnly(trimmed))
            throw new DomainRuleException("search.name_invalid");
        return trimmed;
    }

    public static (int? From, int? To) RequireYears(int? from, int? to)
    {
        if (!YearInRange(from) || !YearInRange(to) || (from is int start && to is int end && start > end))
            throw new DomainRuleException("search.year_invalid");
        return (from, to);
    }

    public static string? EpitaphOf(string? epitaph)
    {
        if (string.IsNullOrWhiteSpace(epitaph))
            return null;
        var trimmed = epitaph.Trim();
        if (trimmed.Length > EpitaphMaxLength)
            throw new DomainRuleException("memorial.epitaph_invalid");
        return trimmed;
    }

    public static string? PhotoOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var trimmed = url.Trim();
        if (trimmed.Length > PhotoUrlMaxLength || !Http(trimmed))
            throw new DomainRuleException("memorial.photo_invalid");
        return trimmed;
    }

    private static bool Revealed(DateOnly diedOn, int days, DateOnly today)
    {
        try
        {
            return diedOn.AddDays(days) <= today;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool YearInRange(int? year) => year is null or >= 1 and <= 9999;

    private static bool LettersOnly(string name)
    {
        foreach (var character in name)
        {
            if (!char.IsLetter(character) && character is not (' ' or '-' or '\'' or '.'))
                return false;
        }

        return true;
    }

    private static bool Http(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http";
}
