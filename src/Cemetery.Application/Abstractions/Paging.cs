using Cemetery.Domain.Identity;

namespace Cemetery.Application.Abstractions;

public sealed record PageRequest(string? Cursor, int? Limit)
{
    public static PageRequest First { get; } = new(null, null);
}

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class Pages
{
    public const int DefaultLimit = 50;

    public const int MaxLimit = 100;

    public static int Size(int? limit)
    {
        var size = limit ?? DefaultLimit;
        if (size < 1 || size > MaxLimit)
            throw new DomainRuleException("page.limit_invalid");

        return size;
    }
}

public static class KeysetCursor
{
    public static string Encode(string sort, Guid id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sort + "\u001f" + id));

    public static (string Sort, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        var text = Text(cursor);
        var split = text.LastIndexOf('\u001f');
        if (split <= 0 || !Guid.TryParse(text[(split + 1)..], out var id))
            throw new DomainRuleException("page.cursor_invalid");

        return (text[..split], id);
    }

    private static string Text(string cursor)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException)
        {
            throw new DomainRuleException("page.cursor_invalid");
        }
    }
}

public sealed record DeceasedFilter(string? Query, Guid? SectionId, DateOnly? DiedFrom, DateOnly? DiedTo, bool WithoutSite)
{
    public static DeceasedFilter None { get; } = new(null, null, null, null, false);
}

public sealed record IntermentFilter(string? Query, Guid? GraveSiteId, DateOnly? BuriedFrom, DateOnly? BuriedTo)
{
    public static IntermentFilter None { get; } = new(null, null, null, null);
}

public sealed record ChargeFilter(string? Query, string? Status, int? Year)
{
    public static ChargeFilter None { get; } = new(null, null, null);
}

public sealed record WorkOrderFilter(string? Query, string? Status)
{
    public static WorkOrderFilter None { get; } = new(null, null);
}
