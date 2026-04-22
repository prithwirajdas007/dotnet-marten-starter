namespace MartenStarter.Domain.Events;

public record TradeOrderAmended(
    Guid OrderId,
    decimal NewQuantity,
    DateTimeOffset OccurredAt);
