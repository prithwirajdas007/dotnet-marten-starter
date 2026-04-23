using Marten.Events.Aggregation;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;

namespace MartenStarter.Domain.Projections;

// Keeps the TradeOrderSummary read model in sync with events from a single trade
// stream. Registered as Inline in Program.cs, so updates happen in the same Marten
// transaction as the event append — either both land or neither does.
//
// Create() runs on the stream's very first event. Apply(event, summary) runs on
// everything after that.
public class TradeOrderSummaryProjection : SingleStreamProjection<TradeOrderSummary, Guid>
{
    public TradeOrderSummary Create(TradeOrderCreated e) => new()
    {
        Id = e.OrderId,
        Instrument = e.Instrument,
        Quantity = e.Quantity,
        Side = e.Side,
        Status = TradeStatus.Created,
        CreatedAt = e.OccurredAt,
        LastUpdated = e.OccurredAt
    };

    public void Apply(TradeOrderPriced e, TradeOrderSummary s)
    {
        s.QuotedPrice = e.Price;
        s.Currency = e.Currency;
        s.Status = TradeStatus.Priced;
        s.LastUpdated = e.OccurredAt;
    }

    public void Apply(TradeOrderExecuted e, TradeOrderSummary s)
    {
        s.ExecutionPrice = e.ExecutionPrice;
        s.Counterparty = e.Counterparty;
        s.Status = TradeStatus.Executed;
        s.LastUpdated = e.OccurredAt;
    }

    public void Apply(TradeOrderCancelled e, TradeOrderSummary s)
    {
        s.CancellationReason = e.Reason;
        s.Status = TradeStatus.Cancelled;
        s.LastUpdated = e.OccurredAt;
    }

    public void Apply(TradeOrderAmended e, TradeOrderSummary s)
    {
        s.Quantity = e.NewQuantity;
        s.LastUpdated = e.OccurredAt;
    }
}
