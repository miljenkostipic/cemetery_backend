using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;

namespace Cemetery.Application.Tests;

internal static class Callers
{
    public static ICallerContext For(Guid organizationId, MembershipRole role) =>
        new CallerStub(Guid.CreateVersion7(), organizationId, role);
}

internal sealed class CallerStub(Guid userId, Guid organizationId, MembershipRole role) : ICallerContext
{
    public Guid UserId { get; } = userId;

    public Guid OrganizationId { get; } = organizationId;

    public MembershipRole Role { get; } = role;
}
