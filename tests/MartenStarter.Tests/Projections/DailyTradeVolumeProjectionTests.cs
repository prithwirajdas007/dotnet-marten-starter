using MartenStarter.Domain.Events;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Tests.Projections;

// Same approach as the single-stream projection tests — exercise Apply directly,
// no Marten, no database. For the cross-stream case, different event streams can
// feed the same DailyTradeVolume doc, so we just reuse one doc instance across
// Apply calls like Marten would after looking the row up by key.
public class DailyTradeVolumeProjectionTests
{
    private static readonly DateTimeOffset Apr23_0900 = new(2026, 4, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly DailyTradeVolumeProjection _projection = new();

    [Fact]
    public void First_apply_sets_identity_date_and_instrument()
    {
        var dv = new DailyTradeVolume();

        _projection.Apply(Created("USDCAD", 1_000_000m, Apr23_0900), dv);

        Assert.Equal("2026-04-23:USDCAD", dv.Id);
        Assert.Equal(new DateOnly(2026, 4, 23), dv.TradeDate);
        Assert.Equal("USDCAD", dv.Instrument);
        Assert.Equal(1, dv.TradeCount);
        Assert.Equal(1_000_000m, dv.TotalNotional);
        Assert.Equal(Apr23_0900, dv.LastUpdated);
    }

    [Fact]
    public void Two_trades_same_key_aggregate_into_one_doc()
    {
        var dv = new DailyTradeVolume();

        _projection.Apply(Created("USDCAD", 1_000_000m, Apr23_0900), dv);
        _projection.Apply(Created("USDCAD", 500_000m, Apr23_0900.AddHours(3)), dv);

        Assert.Equal(2, dv.TradeCount);
        Assert.Equal(1_500_000m, dv.TotalNotional);
        Assert.Equal(Apr23_0900.AddHours(3), dv.LastUpdated);
    }

    [Fact]
    public void Different_instruments_would_produce_different_keys()
    {
        // We can't test routing inside a unit test (that's Marten's job), but we
        // can at least verify the key format is stable and distinct per instrument.
        var dvUsd = new DailyTradeVolume();
        var dvEur = new DailyTradeVolume();

        _projection.Apply(Created("USDCAD", 1m, Apr23_0900), dvUsd);
        _projection.Apply(Created("EURUSD", 1m, Apr23_0900), dvEur);

        Assert.NotEqual(dvUsd.Id, dvEur.Id);
        Assert.Equal("2026-04-23:USDCAD", dvUsd.Id);
        Assert.Equal("2026-04-23:EURUSD", dvEur.Id);
    }

    [Fact]
    public void Different_days_would_produce_different_keys()
    {
        var day1 = new DailyTradeVolume();
        var day2 = new DailyTradeVolume();

        _projection.Apply(Created("USDCAD", 1m, Apr23_0900), day1);
        _projection.Apply(Created("USDCAD", 1m, Apr23_0900.AddDays(1)), day2);

        Assert.Equal("2026-04-23:USDCAD", day1.Id);
        Assert.Equal("2026-04-24:USDCAD", day2.Id);
    }

    private static TradeOrderCreated Created(string instrument, decimal quantity, DateTimeOffset at)
        => new(Guid.NewGuid(), instrument, quantity, Side.Buy, at);
}
