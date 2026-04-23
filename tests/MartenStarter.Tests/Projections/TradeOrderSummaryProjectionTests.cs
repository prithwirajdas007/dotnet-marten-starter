using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Tests.Projections;

// Projections are just event-to-read-model transforms. We can exercise Create and
// Apply directly — no Marten, no database. If these tests pass, the projection's
// logic is right; Marten's job is only to invoke them at the right time.
public class TradeOrderSummaryProjectionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly TradeOrderSummaryProjection _projection = new();

    [Fact]
    public void Create_copies_fields_from_created_event()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1_000_000m, Side.Buy, T0));

        Assert.Equal(OrderId, s.Id);
        Assert.Equal("USDCAD", s.Instrument);
        Assert.Equal(1_000_000m, s.Quantity);
        Assert.Equal(Side.Buy, s.Side);
        Assert.Equal(TradeStatus.Created, s.Status);
        Assert.Equal(T0, s.CreatedAt);
        Assert.Equal(T0, s.LastUpdated);
    }

    [Fact]
    public void Applying_priced_updates_quote_and_status()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        _projection.Apply(new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)), s);

        Assert.Equal(1.37m, s.QuotedPrice);
        Assert.Equal("CAD", s.Currency);
        Assert.Equal(TradeStatus.Priced, s.Status);
        Assert.Equal(T0.AddMinutes(1), s.LastUpdated);
    }

    [Fact]
    public void Applying_executed_fills_execution_fields()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));
        _projection.Apply(new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)), s);

        _projection.Apply(new TradeOrderExecuted(OrderId, 1.371m, "BankX", T0.AddMinutes(2)), s);

        Assert.Equal(1.371m, s.ExecutionPrice);
        Assert.Equal("BankX", s.Counterparty);
        Assert.Equal(TradeStatus.Executed, s.Status);
        Assert.Equal(T0.AddMinutes(2), s.LastUpdated);
    }

    [Fact]
    public void Applying_cancelled_records_reason_and_status()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        _projection.Apply(new TradeOrderCancelled(OrderId, "client withdrew", T0.AddMinutes(1)), s);

        Assert.Equal("client withdrew", s.CancellationReason);
        Assert.Equal(TradeStatus.Cancelled, s.Status);
    }

    [Fact]
    public void Applying_amended_updates_quantity_without_changing_status()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        _projection.Apply(new TradeOrderAmended(OrderId, 2_000_000m, T0.AddMinutes(1)), s);

        Assert.Equal(2_000_000m, s.Quantity);
        Assert.Equal(TradeStatus.Created, s.Status);
        Assert.Equal(T0.AddMinutes(1), s.LastUpdated);
    }

    [Fact]
    public void Full_lifecycle_through_projection_matches_aggregate_shape()
    {
        var s = _projection.Create(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));
        _projection.Apply(new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)), s);
        _projection.Apply(new TradeOrderExecuted(OrderId, 1.371m, "BankX", T0.AddMinutes(2)), s);

        Assert.Equal(TradeStatus.Executed, s.Status);
        Assert.Equal(1.37m, s.QuotedPrice);
        Assert.Equal(1.371m, s.ExecutionPrice);
        Assert.Equal("BankX", s.Counterparty);
    }
}
