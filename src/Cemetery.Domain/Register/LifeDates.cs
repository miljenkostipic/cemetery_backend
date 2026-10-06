using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Register;

public static class LifeDates
{
    public static void RequireOrder(DateOnly? bornOn, DateOnly? diedOn)
    {
        if (bornOn is not null && diedOn is not null && bornOn > diedOn)
            throw new DomainRuleException("deceased.dates_invalid");
    }

    public static void RequireBurial(DateOnly? diedOn, DateOnly buriedOn)
    {
        if (diedOn is not null && buriedOn < diedOn)
            throw new DomainRuleException("interment.date_invalid");
    }
}

public static class CorrectionReason
{
    public const int MaxLength = 500;

    public static string Require(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("deceased.reason_required");

        var trimmed = reason.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainRuleException("deceased.reason_required");

        return trimmed;
    }
}

public static class RestPeriod
{
    public const int DefaultYears = 15;
    public const int MinimumYears = 1;
    public const int MaximumYears = 99;

    public static int Require(int years)
    {
        if (years < MinimumYears || years > MaximumYears)
            throw new DomainRuleException("cemetery.rest_period_invalid");

        return years;
    }

    public static bool HasElapsed(DateOnly buriedOn, DateOnly on, int years) =>
        on >= buriedOn.AddYears(Require(years));
}
