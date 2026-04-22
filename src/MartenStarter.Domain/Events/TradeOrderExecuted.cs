namespace MartenStarter.Domain.Events;

public record TradeOrderExecuted(
    Guid OrderId,
    decimal ExecutionPrice,
    string Counterparty,
    DateTimeOffset OccurredAt);
