using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;

namespace MartenStarter.Api.Endpoints;

public record SeedResponse(int TradesCreated);

// Drops five sample trades across every lifecycle state so reviewers can see the
// system populated without clicking through MartenStarter.Api.http one trade at
// a time. Demo-only — appends events directly instead of going through the
// aggregate's business methods, since we control the inputs.
public sealed class SeedEndpoint(IDocumentSession session)
    : EndpointWithoutRequest<SeedResponse>
{
    public override void Configure()
    {
        Post("/api/seed");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Created — just placed.
        var t1 = Guid.NewGuid();
        session.Events.StartStream<TradeOrder>(t1,
            new TradeOrderCreated(t1, "USDCAD", 1_000_000m, Side.Buy, now));

        // Priced — quote attached.
        var t2 = Guid.NewGuid();
        session.Events.StartStream<TradeOrder>(t2,
            new TradeOrderCreated(t2, "EURUSD", 500_000m, Side.Sell, now),
            new TradeOrderPriced(t2, 1.08m, "USD", now.AddSeconds(1)));

        // Executed — full lifecycle.
        var t3 = Guid.NewGuid();
        session.Events.StartStream<TradeOrder>(t3,
            new TradeOrderCreated(t3, "GBPUSD", 250_000m, Side.Buy, now),
            new TradeOrderPriced(t3, 1.25m, "USD", now.AddSeconds(1)),
            new TradeOrderExecuted(t3, 1.251m, "BankX", now.AddSeconds(2)));

        // Cancelled — pulled before pricing.
        var t4 = Guid.NewGuid();
        session.Events.StartStream<TradeOrder>(t4,
            new TradeOrderCreated(t4, "JPYUSD", 100_000m, Side.Sell, now),
            new TradeOrderCancelled(t4, "client withdrew", now.AddSeconds(1)));

        // Amended — quantity changed, still Created.
        var t5 = Guid.NewGuid();
        session.Events.StartStream<TradeOrder>(t5,
            new TradeOrderCreated(t5, "AUDUSD", 100_000m, Side.Buy, now),
            new TradeOrderAmended(t5, 750_000m, now.AddSeconds(1)));

        await session.SaveChangesAsync(ct);

        await Send.OkAsync(new SeedResponse(5), ct);
    }
}
