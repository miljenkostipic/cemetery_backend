using System.Globalization;
using System.Text;

namespace Cemetery.Domain.Identity;

public static class OrganizationSlug
{
    public static string FromName(string name)
    {
        var decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var buffer = new char[decomposed.Length];
        var length = 0;
        var pendingDash = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            length = Append(buffer, length, ref pendingDash, character);
        }

        if (length > 0 && buffer[length - 1] == '-')
            length--;

        if (length == 0)
            throw new DomainRuleException("organization.slug_empty");

        return new string(buffer, 0, length);
    }

    private static int Append(char[] buffer, int length, ref bool pendingDash, char character)
    {
        if (char.IsAsciiLetterOrDigit(character))
        {
            buffer[length] = character;
            pendingDash = false;
            return length + 1;
        }

        if (length == 0 || pendingDash)
            return length;

        buffer[length] = '-';
        pendingDash = true;
        return length + 1;
    }
}
