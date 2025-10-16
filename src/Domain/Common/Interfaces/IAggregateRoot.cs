namespace SSW_x_Vonage_Clean_Architecture.Domain.Common.Interfaces;

public interface IAggregateRoot
{
    void AddDomainEvent(IDomainEvent domainEvent);

    IReadOnlyList<IDomainEvent> PopDomainEvents();
}