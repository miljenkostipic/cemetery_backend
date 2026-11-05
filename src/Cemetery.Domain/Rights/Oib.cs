using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Rights;

public static class OibNumber
{
    public const int Length = 11;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = raw.Trim();
        if (digits.Length != Length || !IsChecksum(digits))
            throw new DomainRuleException("holder.oib_invalid");

        return digits;
    }

    private static bool IsChecksum(string digits)
    {
        foreach (var character in digits)
        {
            if (character is < '0' or > '9')
                return false;
        }

        return CheckDigit(digits) == digits[^1] - '0';
    }

    private static int CheckDigit(string digits)
    {
        var remainder = 10;
        for (var index = 0; index < Length - 1; index++)
            remainder = Step(remainder, digits[index] - '0');

        var check = 11 - remainder;
        return check == 10 ? 0 : check;
    }

    private static int Step(int remainder, int digit)
    {
        var sum = (remainder + digit) % 10;
        if (sum == 0)
            sum = 10;

        return sum * 2 % 11;
    }
}
