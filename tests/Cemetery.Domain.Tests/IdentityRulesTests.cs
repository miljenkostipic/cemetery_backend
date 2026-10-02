using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Tests;

public sealed class OrganizationSlugTests
{
    [Fact]
    public void Strips_diacritics_and_spaces()
    {
        var slug = OrganizationSlug.FromName("Groblje Šestine");

        Assert.Equal("groblje-sestine", slug);
    }

    [Fact]
    public void Rejects_a_name_with_no_letters()
    {
        var error = Assert.Throws<DomainRuleException>(() => OrganizationSlug.FromName("!!!"));

        Assert.Equal("organization.slug_empty", error.Code);
    }
}

public sealed class OrganizationTests
{
    [Fact]
    public void Creates_a_graveyard_operator()
    {
        var createdAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var organization = Organization.Create("Alpha", "alpha", createdAt);

        Assert.Equal("Alpha", organization.Name);
        Assert.Equal("alpha", organization.Slug);
        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal(createdAt, organization.CreatedAt);
    }

    [Fact]
    public void Rejects_a_short_name()
    {
        var error = Assert.Throws<DomainRuleException>(() => Organization.Create("A", "a", DateTimeOffset.UnixEpoch));

        Assert.Equal("organization.name_invalid", error.Code);
    }
}

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepts_a_matching_holder()
    {
        var invitation = Issue();

        invitation.Accept(EmailAddress.Parse("holder@example.com"), Now);

        Assert.Equal(Now, invitation.AcceptedAt);
    }

    [Fact]
    public void Rejects_the_wrong_email()
    {
        var invitation = Issue();

        var error = Assert.Throws<DomainRuleException>(() => invitation.Accept(EmailAddress.Parse("other@example.com"), Now));

        Assert.Equal("invitation.email_mismatch", error.Code);
    }

    [Fact]
    public void Rejects_an_expired_invitation()
    {
        var invitation = Issue();

        var error = Assert.Throws<DomainRuleException>(() => invitation.Accept(EmailAddress.Parse("holder@example.com"), Now.AddDays(8)));

        Assert.Equal("invitation.expired", error.Code);
    }

    [Fact]
    public void Rejects_a_second_acceptance()
    {
        var invitation = Issue();
        invitation.Accept(EmailAddress.Parse("holder@example.com"), Now);

        var error = Assert.Throws<DomainRuleException>(() => invitation.Accept(EmailAddress.Parse("holder@example.com"), Now));

        Assert.Equal("invitation.already_accepted", error.Code);
    }

    private static Invitation Issue() =>
        Invitation.Issue(
            Guid.CreateVersion7(),
            EmailAddress.Parse("holder@example.com"),
            MembershipRole.GraveHolder,
            "hash",
            Now,
            Now.Add(Invitation.Lifetime));
}

public sealed class MembershipTests
{
    [Fact]
    public void Rejects_an_empty_user()
    {
        var error = Assert.Throws<DomainRuleException>(() => Membership.Create(Guid.CreateVersion7(), Guid.Empty, MembershipRole.CemeteryClerk));

        Assert.Equal("membership.invalid", error.Code);
    }
}

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Accepts_a_mixed_password() => Assert.True(PasswordPolicy.IsSatisfied("Correct-Horse-1"));

    [Fact]
    public void Accepts_eight_characters() => Assert.True(PasswordPolicy.IsSatisfied("Abcdefg1"));

    [Fact]
    public void Rejects_a_short_password() => Assert.False(PasswordPolicy.IsSatisfied("Short-1"));
}
