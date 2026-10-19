using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Launch;

public sealed class MaticaLink : TenantEntity
{
    private MaticaLink()
    {
    }

    public Guid CemeteryId { get; private set; }

    public Guid IntermentId { get; private set; }

    public Guid DeathCertificateId { get; private set; }

    public static MaticaLink Create(Guid tenantId, Guid cemeteryId, Guid intermentId, Guid deathCertificateId)
    {
        if (tenantId == Guid.Empty || cemeteryId == Guid.Empty)
            throw new DomainRuleException("matica.not_found");
        if (intermentId == Guid.Empty)
            throw new DomainRuleException("matica.interment_invalid");
        if (deathCertificateId == Guid.Empty)
            throw new DomainRuleException("matica.certificate_invalid");

        return new MaticaLink
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            IntermentId = intermentId,
            DeathCertificateId = deathCertificateId,
        };
    }
}
