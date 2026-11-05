using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Rights;

public sealed class StoredFile : TenantEntity
{
    private StoredFile()
    {
        Content = [];
    }

    public byte[] Content { get; private set; }

    public static StoredFile Keep(Guid tenantId, byte[] content)
    {
        if (tenantId == Guid.Empty || content.Length == 0)
            throw new DomainRuleException("document.empty");

        return new StoredFile
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Content = content,
        };
    }
}
