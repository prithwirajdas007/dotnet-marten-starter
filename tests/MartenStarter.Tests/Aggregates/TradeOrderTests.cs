using MartenStarter.Domain;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;

namespace MartenStarter.Tests.Aggregates;

// No database, no Docker, no Marten. The aggregate is a POCO — that's the whole point.
public class TradeOrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ---------- Create ----------

    [Fact]
    public void Create_returns_event_with_supplied_values()
    {
        var e = TradeOrder.Create(OrderId, "USDCAD", 1_000_000m, Side.Buy, T0);

        Assert.Equal(OrderId, e.OrderId);
        Assert.Equal("USDCAD", e.Instrument);
        Assert.Equal(1_000_000m, e.Quantity);
        Assert.Equal(Side.Buy, e.Side);
        Assert.Equal(T0, e.OccurredAt);
    }

    [Fact]
    public void Create_rejects_empty_id()
    {
        Assert.Throws<ArgumentException>(() =>
            TradeOrder.Create(Guid.Empty, "USDCAD", 1m, Side.Buy, T0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_instrument(string instrument)
    {
        Assert.Throws<ArgumentException>(() =>
            TradeOrder.Create(OrderId, instrument, 1m, Side.Buy, T0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_quantity(decimal quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TradeOrder.Create(OrderId, "USDCAD", quantity, Side.Buy, T0));
    }

    // ---------- Replay rebuilds state (the core promise of event sourcing) ----------

    [Fact]
    public void Replaying_created_event_hydrates_aggregate()
    {
        var trade = Replay(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        Assert.Equal(OrderId, trade.Id);
        Assert.Equal("USDCAD", trade.Instrument);
        Assert.Equal(1m, trade.Quantity);
        Assert.Equal(Side.Buy, trade.Side);
        Assert.Equal(TradeStatus.Created, trade.Status);
        Assert.Equal(T0, trade.CreatedAt);
        Assert.Equal(T0, trade.LastUpdated);
    }

    [Fact]
    public void Full_lifecycle_replay_ends_in_executed_state()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)),
            new TradeOrderExecuted(OrderId, 1.371m, "BankX", T0.AddMinutes(2)));

        Assert.Equal(TradeStatus.Executed, trade.Status);
        Assert.Equal(1.37m, trade.QuotedPrice);
        Assert.Equal("CAD", trade.Currency);
        Assert.Equal(1.371m, trade.ExecutionPrice);
        Assert.Equal("BankX", trade.Counterparty);
        Assert.Equal(T0.AddMinutes(2), trade.LastUpdated);
    }

    [Fact]
    public void Amending_before_pricing_updates_quantity_but_preserves_status()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderAmended(OrderId, 2m, T0.AddMinutes(1)));

        Assert.Equal(2m, trade.Quantity);
        Assert.Equal(TradeStatus.Created, trade.Status);
    }

    // ---------- Price ----------

    [Fact]
    public void Price_on_created_returns_priced_event()
    {
        var trade = Replay(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        var e = trade.Price(1.37m, "CAD", T0.AddMinutes(1));

        Assert.Equal(OrderId, e.OrderId);
        Assert.Equal(1.37m, e.Price);
        Assert.Equal("CAD", e.Currency);
    }

    [Fact]
    public void Price_on_already_priced_trade_throws()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)));

        Assert.Throws<DomainException>(() => trade.Price(2m, "CAD", T0.AddMinutes(2)));
    }

    [Fact]
    public void Price_on_executed_trade_throws()
    {
        var trade = FullyExecuted();

        Assert.Throws<DomainException>(() => trade.Price(2m, "CAD", T0.AddMinutes(3)));
    }

    [Fact]
    public void Price_on_cancelled_trade_throws()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderCancelled(OrderId, "client withdrew", T0.AddMinutes(1)));

        Assert.Throws<DomainException>(() => trade.Price(2m, "CAD", T0.AddMinutes(2)));
    }

    // ---------- Execute ----------

    [Fact]
    public void Execute_on_priced_returns_executed_event()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)));

        var e = trade.Execute(1.371m, "BankX", T0.AddMinutes(2));

        Assert.Equal(1.371m, e.ExecutionPrice);
        Assert.Equal("BankX", e.Counterparty);
    }

    [Fact]
    public void Execute_on_created_throws()
    {
        var trade = Replay(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        Assert.Throws<DomainException>(() => trade.Execute(1m, "BankX", T0.AddMinutes(1)));
    }

    [Fact]
    public void Execute_on_cancelled_throws()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderCancelled(OrderId, "client withdrew", T0.AddMinutes(1)));

        Assert.Throws<DomainException>(() => trade.Execute(1m, "BankX", T0.AddMinutes(2)));
    }

    // ---------- Cancel ----------

    [Fact]
    public void Cancel_on_created_returns_cancelled_event()
    {
        var trade = Replay(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        var e = trade.Cancel("client withdrew", T0.AddMinutes(1));

        Assert.Equal("client withdrew", e.Reason);
    }

    [Fact]
    public void Cancel_on_priced_returns_cancelled_event()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)));

        var e = trade.Cancel("pricing stale", T0.AddMinutes(2));

        Assert.Equal("pricing stale", e.Reason);
    }

    [Fact]
    public void Cancel_on_executed_throws()
    {
        var trade = FullyExecuted();

        Assert.Throws<DomainException>(() => trade.Cancel("too late", T0.AddMinutes(3)));
    }

    [Fact]
    public void Cancel_on_cancelled_throws()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderCancelled(OrderId, "first time", T0.AddMinutes(1)));

        Assert.Throws<DomainException>(() => trade.Cancel("second time", T0.AddMinutes(2)));
    }

    // ---------- Amend ----------

    [Fact]
    public void Amend_on_priced_returns_amended_event()
    {
        var trade = Replay(
            new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
            new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)));

        var e = trade.Amend(2m, T0.AddMinutes(2));

        Assert.Equal(2m, e.NewQuantity);
    }

    [Fact]
    public void Amend_on_executed_throws()
    {
        var trade = FullyExecuted();

        Assert.Throws<DomainException>(() => trade.Amend(2m, T0.AddMinutes(3)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Amend_rejects_non_positive_quantity(decimal newQuantity)
    {
        var trade = Replay(new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0));

        Assert.Throws<ArgumentOutOfRangeException>(() => trade.Amend(newQuantity, T0.AddMinutes(1)));
    }

    // ---------- helpers ----------

    // Manually replays events onto a fresh aggregate, the same way Marten does via reflection.
    // `dynamic` picks the right Apply overload at runtime — keeps the helper tiny.
    private static TradeOrder Replay(params object[] events)
    {
        var trade = new TradeOrder();
        foreach (var e in events)
        {
            ((dynamic)trade).Apply((dynamic)e);
        }
        return trade;
    }

    private static TradeOrder FullyExecuted() => Replay(
        new TradeOrderCreated(OrderId, "USDCAD", 1m, Side.Buy, T0),
        new TradeOrderPriced(OrderId, 1.37m, "CAD", T0.AddMinutes(1)),
        new TradeOrderExecuted(OrderId, 1.371m, "BankX", T0.AddMinutes(2)));
}
