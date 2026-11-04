namespace Cemetery.Application.Register;

internal static class RegisterClock
{
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
