namespace MartenStarter.Domain.Events;

public record TradeOrderCreated(
    Guid OrderId,
    string Instrument,
    decimal Quantity,
    Side Side,
    DateTimeOffset OccurredAt);
