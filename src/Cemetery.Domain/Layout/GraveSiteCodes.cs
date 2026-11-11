using System.Globalization;

namespace Cemetery.Domain.Layout;

/// <summary>
/// Grave codes read field-row-place: <c>A-03-12</c> is field A, row 3, place 12. Graves outside a
/// row (irregular old parts) read field-number: <c>C-047</c>. Numbers are zero padded (two digits
/// for rows and places, three for row-less numbers) and simply grow wider past the padding.
/// </summary>
public static class GraveSiteCodes
{
    private const string RowFormat = "00";
    private const string LooseFormat = "000";
    private const int MaxNumberDigits = 9;

    public static string InRow(string sectionCode, string rowLabel, int place) =>
        $"{RowPrefix(sectionCode, rowLabel)}{place.ToString(RowFormat, CultureInfo.InvariantCulture)}";

    public static string Loose(string sectionCode, int number) =>
        $"{LoosePrefix(sectionCode)}{number.ToString(LooseFormat, CultureInfo.InvariantCulture)}";

    /// <summary>A numeric row label is zero padded ("3" reads "03"); any other label is kept as it is.</summary>
    public static string RowPart(string rowLabel)
    {
        var label = rowLabel.Trim();
        return Number(label) is { } number ? number.ToString(RowFormat, CultureInfo.InvariantCulture) : label;
    }

    /// <summary>
    /// The place after the highest one already coded in the row, skipping any code the cemetery
    /// already uses.
    /// </summary>
    public static string NextInRow(string sectionCode, string rowLabel, IEnumerable<string> rowCodes, IReadOnlySet<string> taken)
    {
        var place = Highest(RowPrefix(sectionCode, rowLabel), rowCodes) + 1;
        while (taken.Contains(InRow(sectionCode, rowLabel, place)))
            place++;
        return InRow(sectionCode, rowLabel, place);
    }

    /// <summary>
    /// The number after the highest one among the section's row-less graves, skipping any code the
    /// cemetery already uses.
    /// </summary>
    public static string NextLoose(string sectionCode, IEnumerable<string> looseCodes, IReadOnlySet<string> taken)
    {
        var number = Highest(LoosePrefix(sectionCode), looseCodes) + 1;
        while (taken.Contains(Loose(sectionCode, number)))
            number++;
        return Loose(sectionCode, number);
    }

    private static string RowPrefix(string sectionCode, string rowLabel) => $"{sectionCode}-{RowPart(rowLabel)}-";

    private static string LoosePrefix(string sectionCode) => $"{sectionCode}-";

    private static int Highest(string prefix, IEnumerable<string> codes)
    {
        var highest = 0;
        foreach (var code in codes)
        {
            if (code.StartsWith(prefix, StringComparison.Ordinal) && Number(code[prefix.Length..]) is { } number)
                highest = Math.Max(highest, number);
        }

        return highest;
    }

    private static int? Number(string text)
    {
        if (text.Length == 0 || text.Length > MaxNumberDigits || !text.All(char.IsAsciiDigit))
            return null;
        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
