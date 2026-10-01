namespace Cemetery.Domain.Identity;

public enum MembershipRole
{
    OrganizationAdmin = 1,
    CemeteryClerk = 2,
    FieldWorker = 3,
    GraveHolder = 4,
}

public static class MembershipRoles
{
    public const string OrganizationAdmin = "organization_admin";
    public const string CemeteryClerk = "cemetery_clerk";
    public const string FieldWorker = "field_worker";
    public const string GraveHolder = "grave_holder";

    public static string ToCode(MembershipRole role) => role switch
    {
        MembershipRole.OrganizationAdmin => OrganizationAdmin,
        MembershipRole.CemeteryClerk => CemeteryClerk,
        MembershipRole.FieldWorker => FieldWorker,
        MembershipRole.GraveHolder => GraveHolder,
        _ => throw new DomainRuleException("membership.role_invalid"),
    };

    public static bool TryParse(string? code, out MembershipRole role)
    {
        (var parsed, role) = code switch
        {
            OrganizationAdmin => (true, MembershipRole.OrganizationAdmin),
            CemeteryClerk => (true, MembershipRole.CemeteryClerk),
            FieldWorker => (true, MembershipRole.FieldWorker),
            GraveHolder => (true, MembershipRole.GraveHolder),
            _ => (false, default),
        };
        return parsed;
    }
}
