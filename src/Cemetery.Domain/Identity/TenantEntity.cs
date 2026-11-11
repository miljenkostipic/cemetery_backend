using System.ComponentModel.DataAnnotations.Schema;

namespace Cemetery.Domain.Identity;

public abstract class TenantEntity
{
    private readonly List<DomainEvent> _events = [];

    public Guid Id { get; protected set; }

    public Guid TenantId { get; protected set; }

    [NotMapped]
    public IReadOnlyList<DomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    protected void Raise(DomainEvent domainEvent) => _events.Add(domainEvent);
}
