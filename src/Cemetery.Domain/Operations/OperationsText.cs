using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public static class OperationsText
{
    public const int NoteMaxLength = 500;
    public const int NameMaxLength = 80;

    public static string Note(string? value, string code)
    {
        if (value is null)
            return "";

        RejectControls(value, code);
        var text = value.Trim();
        if (text.Length > NoteMaxLength || StartsLikeFormula(text))
            throw new DomainRuleException(code);
        return text;
    }

    public static string Name(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainRuleException(code);

        RejectControls(value, code);
        var text = value.Trim();
        if (text.Length > NameMaxLength || StartsLikeFormula(text))
            throw new DomainRuleException(code);
        return text;
    }

    private static void RejectControls(string value, string code)
    {
        foreach (var character in value)
        {
            if (character < ' ' || character == '\u007f')
                throw new DomainRuleException(code);
        }
    }

    private static bool StartsLikeFormula(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@';
}
