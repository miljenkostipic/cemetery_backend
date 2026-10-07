using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Rights;

public sealed class OfficeRequest : TenantEntity
{
    public const int MaxMessageLength = 2000;

    private OfficeRequest()
    {
        Message = "";
    }

    public Guid AuthorId { get; private set; }

    public string Message { get; private set; }

    public Guid? GraveSiteId { get; private set; }

    public DateOnly CreatedOn { get; private set; }

    public static OfficeRequest Send(Guid tenantId, Guid authorId, string message, Guid? graveSiteId, DateOnly createdOn)
    {
        var text = message.Trim();
        if (tenantId == Guid.Empty || authorId == Guid.Empty || text.Length < 1 || text.Length > MaxMessageLength)
            throw new DomainRuleException("request.message_invalid");
        if (graveSiteId == Guid.Empty)
            throw new DomainRuleException("grave_site.not_found");

        return new OfficeRequest
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AuthorId = authorId,
            Message = text,
            GraveSiteId = graveSiteId,
            CreatedOn = createdOn,
        };
    }
}
