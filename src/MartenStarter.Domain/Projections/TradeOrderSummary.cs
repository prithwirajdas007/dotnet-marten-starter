using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;

namespace MartenStarter.Domain.Projections;

// Read model for GET /api/trades/{id}. Shape mirrors the aggregate today, but that's
// a coincidence — in a richer domain this would carry denormalised fields (e.g. the
// counterparty's display name, not just an id) that the aggregate has no business
// knowing about.
//
// Public setters because Marten's serializer needs them to hydrate rows back into
// objects. Read models don't need the same encapsulation as aggregates.
public class TradeOrderSummary
{
    public Guid Id { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Side Side { get; set; }
    public TradeStatus Status { get; set; }
    public decimal? QuotedPrice { get; set; }
    public string? Currency { get; set; }
    public decimal? ExecutionPrice { get; set; }
    public string? Counterparty { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
