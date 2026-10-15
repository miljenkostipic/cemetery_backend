using System.Globalization;
using System.Text.RegularExpressions;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public static partial class CivilTime
{
    public static DateTime Parse(string? value, string code)
    {
        if (!TryParse(value, out var instant))
            throw new DomainRuleException(code);
        return instant;
    }

    public static bool TryParse(string? value, out DateTime instant)
    {
        instant = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = Pattern().Match(value.Trim());
        if (!match.Success)
            return false;

        return TryCompose(match, out instant);
    }

    public static string Format(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static bool TryCompose(Match match, out DateTime instant)
    {
        instant = default;
        var year = Number(match, 1);
        var month = Number(match, 2);
        var day = Number(match, 3);
        var hour = Number(match, 4);
        var minute = Number(match, 5);
        if (year is < 1 or > 9999 || month is < 1 or > 12 || hour > 23 || minute > 59)
            return false;
        if (day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        instant = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return true;
    }

    private static int Number(Match match, int group) =>
        int.Parse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
