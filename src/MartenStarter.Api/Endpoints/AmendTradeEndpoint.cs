using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public record AmendTradeRequest(decimal NewQuantity);

public sealed class AmendTradeEndpoint(IDocumentSession session)
    : Endpoint<AmendTradeRequest, TradeOrderSummary>
{
    public override void Configure()
    {
        Post("/api/trades/{id:guid}/amend");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AmendTradeRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var trade = await session.Events.AggregateStreamAsync<TradeOrder>(id, token: ct);
        if (trade is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var amended = trade.Amend(req.NewQuantity, DateTimeOffset.UtcNow);

        await session.Events.AppendOptimistic(id, amended);
        await session.SaveChangesAsync(ct);

        var summary = await session.LoadAsync<TradeOrderSummary>(id, ct);
        await Send.OkAsync(summary!, ct);
    }
}
