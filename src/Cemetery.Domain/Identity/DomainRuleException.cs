namespace Cemetery.Domain.Identity;

public sealed class DomainRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
