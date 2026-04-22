using MartenStarter.Domain.Events;

namespace MartenStarter.Domain.Aggregates;

// The trade lifecycle aggregate: Created -> Priced -> Executed, with Cancel/Amend along the way.
//
// Two responsibilities, deliberately split:
//   1. Business methods (Create, Price, Execute, Cancel, Amend) validate the transition
//      and *return* the event that should be appended. They do not mutate state.
//   2. Apply overloads mutate state from an event. Marten invokes these via reflection
//      when replaying the stream to rebuild the aggregate.
//
// Keeping decision and state-transition separate means the same Apply path runs whether an event
// is brand new or being replayed from history — no branching, no "is this the first time?" logic.
public sealed class TradeOrder
{
    public Guid Id { get; private set; }
    public string Instrument { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Side Side { get; private set; }
    public TradeStatus Status { get; private set; }
    public decimal? QuotedPrice { get; private set; }
    public string? Currency { get; private set; }
    public decimal? ExecutionPrice { get; private set; }
    public string? Counterparty { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastUpdated { get; private set; }

    // Static factory because the aggregate doesn't exist until this event is appended —
    // there's no `this` to call a method on.
    public static TradeOrderCreated Create(
        Guid id,
        string instrument,
        decimal quantity,
        Side side,
        DateTimeOffset at)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must be non-empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new TradeOrderCreated(id, instrument, quantity, side, at);
    }

    public TradeOrderPriced Price(decimal price, string currency, DateTimeOffset at)
    {
        if (Status != TradeStatus.Created)
            throw new DomainException($"Cannot price a trade in state {Status}. Must still be Created.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new TradeOrderPriced(Id, price, currency, at);
    }

    public TradeOrderExecuted Execute(decimal executionPrice, string counterparty, DateTimeOffset at)
    {
        if (Status != TradeStatus.Priced)
            throw new DomainException($"Cannot execute a trade in state {Status}. Must be Priced first.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionPrice);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterparty);

        return new TradeOrderExecuted(Id, executionPrice, counterparty, at);
    }

    public TradeOrderCancelled Cancel(string reason, DateTimeOffset at)
    {
        if (Status is TradeStatus.Executed or TradeStatus.Cancelled)
            throw new DomainException($"Cannot cancel a trade in state {Status}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new TradeOrderCancelled(Id, reason, at);
    }

    public TradeOrderAmended Amend(decimal newQuantity, DateTimeOffset at)
    {
        if (Status is TradeStatus.Executed or TradeStatus.Cancelled)
            throw new DomainException($"Cannot amend a trade in state {Status}.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newQuantity);

        return new TradeOrderAmended(Id, newQuantity, at);
    }

    // --- Apply overloads: Marten calls these via reflection during stream replay. ---
    // They must be idempotent w.r.t. the event — no IO, no clock reads, no randomness.

    public void Apply(TradeOrderCreated created)
    {
        Id = created.OrderId;
        Instrument = created.Instrument;
        Quantity = created.Quantity;
        Side = created.Side;
        Status = TradeStatus.Created;
        CreatedAt = created.OccurredAt;
        LastUpdated = created.OccurredAt;
    }

    public void Apply(TradeOrderPriced priced)
    {
        QuotedPrice = priced.Price;
        Currency = priced.Currency;
        Status = TradeStatus.Priced;
        LastUpdated = priced.OccurredAt;
    }

    public void Apply(TradeOrderExecuted executed)
    {
        ExecutionPrice = executed.ExecutionPrice;
        Counterparty = executed.Counterparty;
        Status = TradeStatus.Executed;
        LastUpdated = executed.OccurredAt;
    }

    public void Apply(TradeOrderCancelled cancelled)
    {
        CancellationReason = cancelled.Reason;
        Status = TradeStatus.Cancelled;
        LastUpdated = cancelled.OccurredAt;
    }

    public void Apply(TradeOrderAmended amended)
    {
        Quantity = amended.NewQuantity;
        LastUpdated = amended.OccurredAt;
    }
}
