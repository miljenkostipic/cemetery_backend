using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cemetery.Infrastructure.Persistence;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(Organization.NameMaxLength);
        builder.Property(entity => entity.Slug).HasMaxLength(Organization.NameMaxLength);
        builder.Property(entity => entity.PublishesRegister).HasDefaultValue(false);
        builder.Property(entity => entity.HideRecentDeathsDays).HasDefaultValue(0);
        builder.HasIndex(entity => entity.Slug).IsUnique().HasDatabaseName("organizations_slug_key");
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Role)
            .HasConversion(role => MembershipRoles.ToCode(role), code => ParseRole(code))
            .HasMaxLength(32);
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId })
            .IsUnique()
            .HasDatabaseName("memberships_tenant_user_key");
    }

    private static MembershipRole ParseRole(string code) =>
        MembershipRoles.TryParse(code, out var role) ? role : throw new DomainRuleException("membership.role_invalid");
}

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Email)
            .HasConversion(email => email.Value, value => EmailAddress.Parse(value))
            .HasMaxLength(EmailAddress.MaxLength);
        builder.Property(entity => entity.Role)
            .HasConversion(role => MembershipRoles.ToCode(role), code => ParseRole(code))
            .HasMaxLength(32);
        builder.Property(entity => entity.TokenHash).HasMaxLength(64);
        builder.HasIndex(entity => entity.TokenHash).IsUnique().HasDatabaseName("invitations_token_hash_key");
    }

    private static MembershipRole ParseRole(string code) =>
        MembershipRoles.TryParse(code, out var role) ? role : throw new DomainRuleException("membership.role_invalid");
}

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(UserProfileRules.DisplayNameMaxLength);
    }
}
