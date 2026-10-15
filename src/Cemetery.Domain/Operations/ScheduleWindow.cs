using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public static class ScheduleWindow
{
    public static void RequireOrdered(DateTime start, DateTime end, string code)
    {
        if (start >= end)
            throw new DomainRuleException(code);
    }

    public static bool Overlap(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && otherStart < end;
}
