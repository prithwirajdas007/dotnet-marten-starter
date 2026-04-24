using Marten.Events.Projections;
using MartenStarter.Domain.Events;

namespace MartenStarter.Domain.Projections;

// Cross-stream projection: a TradeOrderCreated from stream X contributes to a
// DailyTradeVolume doc keyed by (event date, instrument). Two trades on the same
// day for USDCAD increment the same row even though their streams are different.
//
// Only Created events drive this read model. Amendments and cancellations are
// deliberately out of scope — they'd need a more elaborate key lookup to route
// (neither event carries the instrument), and the teaching point here is the
// cross-stream roll-up, not every possible aggregation.
public class DailyTradeVolumeProjection : MultiStreamProjection<DailyTradeVolume, string>
{
    public DailyTradeVolumeProjection()
    {
        // Tells Marten how to compute the target doc id from each event — that's
        // how a multi-stream projection knows which row to update.
        Identity<TradeOrderCreated>(e => Key(e));
    }

    public void Apply(TradeOrderCreated e, DailyTradeVolume dv)
    {
        var date = DateOnly.FromDateTime(e.OccurredAt.UtcDateTime);
        dv.Id = Key(e);
        dv.TradeDate = date;
        dv.Instrument = e.Instrument;
        dv.TradeCount++;
        dv.TotalNotional += e.Quantity;
        dv.LastUpdated = e.OccurredAt;
    }

    private static string Key(TradeOrderCreated e)
        => $"{DateOnly.FromDateTime(e.OccurredAt.UtcDateTime):yyyy-MM-dd}:{e.Instrument}";
}
