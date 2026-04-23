using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public record PriceTradeRequest(decimal Price, string Currency);

public sealed class PriceTradeEndpoint(IDocumentSession session)
    : Endpoint<PriceTradeRequest, TradeOrderSummary>
{
    public override void Configure()
    {
        Post("/api/trades/{id:guid}/price");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PriceTradeRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var trade = await session.Events.AggregateStreamAsync<TradeOrder>(id, token: ct);
        if (trade is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Aggregate enforces invariants — throws DomainException if the trade
        // isn't in Created state.
        var priced = trade.Price(req.Price, req.Currency, DateTimeOffset.UtcNow);

        // AppendOptimistic pins the stream to the version we loaded at. If another
        // writer moved it forward, SaveChangesAsync throws ConcurrencyException and
        // the error middleware turns it into 409.
        await session.Events.AppendOptimistic(id, priced);
        await session.SaveChangesAsync(ct);

        var summary = await session.LoadAsync<TradeOrderSummary>(id, ct);
        await Send.OkAsync(summary!, ct);
    }
}
