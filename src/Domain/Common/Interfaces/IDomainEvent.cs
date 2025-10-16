using MediatR;

namespace SSW_x_Vonage_Clean_Architecture.Domain.Common.Interfaces;

/// <summary>
/// Can be raised by an AggregateRoot to notify subscribers of a domain event.
/// </summary>
public interface IDomainEvent : INotification;