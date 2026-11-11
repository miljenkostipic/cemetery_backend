namespace Cemetery.Domain.Identity;

public abstract record DomainEvent(string Type, string Payload);
