using Cemetery.Domain.Identity;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Rights;

public sealed class Holder : TenantEntity
{
    public const int AddressMaxLength = 200;

    public const int PhoneMaxLength = 32;

    private Holder()
    {
        GivenName = "";
        FamilyName = "";
    }

    public string GivenName { get; private set; }

    public string FamilyName { get; private set; }

    public string? Oib { get; private set; }

    public string? Address { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public Guid? UserId { get; private set; }

    public string DisplayName => GivenName + " " + FamilyName;

    public static Holder Register(
        Guid tenantId,
        string? givenName,
        string? familyName,
        string? oib,
        string? address,
        string? email,
        string? phone)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");

        var name = PersonName.Create(givenName, familyName);
        return Create(tenantId, name, OibNumber.Normalize(oib), Contact.Address(address), Contact.Email(email), Contact.Phone(phone));
    }

    public void Correct(string? givenName, string? familyName, string? oib, string? address, string? email, string? phone)
    {
        var name = PersonName.Create(givenName, familyName);
        GivenName = name.Given;
        FamilyName = name.Family;
        Oib = OibNumber.Normalize(oib);
        Address = Contact.Address(address);
        Email = Contact.Email(email);
        Phone = Contact.Phone(phone);
    }

    public void Link(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainRuleException("holder.user_invalid");
        if (UserId is Guid linked && linked != userId)
            throw new DomainRuleException("holder.already_linked");

        UserId = userId;
    }

    private static Holder Create(Guid tenantId, PersonName name, string? oib, string? address, string? email, string? phone) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            GivenName = name.Given,
            FamilyName = name.Family,
            Oib = oib,
            Address = address,
            Email = email,
            Phone = phone,
        };
}

public static class Contact
{
    public static string? Address(string? raw) => Text(raw, Holder.AddressMaxLength, "holder.address_invalid");

    public static string? Phone(string? raw) => Text(raw, Holder.PhoneMaxLength, "holder.phone_invalid");

    public static string? Email(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return EmailAddress.Parse(raw).Value;
    }

    private static string? Text(string? raw, int max, string code)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (text.Length > max)
            throw new DomainRuleException(code);

        return text;
    }
}
