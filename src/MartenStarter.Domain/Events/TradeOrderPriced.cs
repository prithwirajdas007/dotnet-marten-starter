namespace MartenStarter.Domain.Events;

public record TradeOrderPriced(
    Guid OrderId,
    decimal Price,
    string Currency,
    DateTimeOffset OccurredAt);
