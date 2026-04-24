namespace MartenStarter.Domain.Projections;

// Read model for GET /api/daily-volume — one row per (TradeDate, Instrument) pair,
// aggregated from TradeOrderCreated events across every stream.
//
// The composite string id "YYYY-MM-DD:INSTRUMENT" encodes the grouping key. Marten
// uses it to find-or-create the right row as each event flows through.
public class DailyTradeVolume
{
    public string Id { get; set; } = string.Empty;
    public DateOnly TradeDate { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public int TradeCount { get; set; }
    public decimal TotalNotional { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
