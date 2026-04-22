namespace MartenStarter.Domain.Events;

public record TradeOrderCancelled(
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAt);
