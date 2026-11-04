using Cemetery.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Cemetery.Api.Tenancy;

public static class Policies
{
    public const string Office = "office";

    public const string Field = "field";

    public const string Member = "member";

    public const string Holder = "holder";

    public static void AddCallerPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Office, policy => policy.RequireAuthenticatedUser().AddRequirements(OfficeRoles()));
        options.AddPolicy(Field, policy => policy.RequireAuthenticatedUser().AddRequirements(FieldRoles()));
        options.AddPolicy(Member, policy => policy.RequireAuthenticatedUser().AddRequirements(MemberRoles()));
        options.AddPolicy(Holder, policy => policy.RequireAuthenticatedUser().AddRequirements(HolderRoles()));
    }

    private static CallerRoleRequirement OfficeRoles() =>
        new(MembershipRole.OrganizationAdmin, MembershipRole.CemeteryClerk);

    private static CallerRoleRequirement FieldRoles() =>
        new(MembershipRole.OrganizationAdmin, MembershipRole.CemeteryClerk, MembershipRole.FieldWorker);

    private static CallerRoleRequirement MemberRoles() =>
        new(
            MembershipRole.OrganizationAdmin,
            MembershipRole.CemeteryClerk,
            MembershipRole.FieldWorker,
            MembershipRole.GraveHolder);

    private static CallerRoleRequirement HolderRoles() => new(MembershipRole.GraveHolder);
}

public sealed class CallerRoleRequirement(params MembershipRole[] allowed) : IAuthorizationRequirement
{
    public bool Allows(MembershipRole role) => allowed.Contains(role);
}

public sealed class CallerRoleHandler(IHttpContextAccessor http) : AuthorizationHandler<CallerRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CallerRoleRequirement requirement)
    {
        if (CallerState.Read(http.HttpContext) is CallerState caller && requirement.Allows(caller.Role))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public sealed class AccessDeniedHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
            return next(context);

        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
            return WriteForbiddenAsync(context);

        return fallback.HandleAsync(next, context, policy, authorizeResult);
    }

    private static Task WriteForbiddenAsync(HttpContext context) =>
        Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "access.forbidden",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "access.forbidden" })
            .ExecuteAsync(context);
}
