using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public record CancelTradeRequest(string Reason);

public sealed class CancelTradeEndpoint(IDocumentSession session)
    : Endpoint<CancelTradeRequest, TradeOrderSummary>
{
    public override void Configure()
    {
        Post("/api/trades/{id:guid}/cancel");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancelTradeRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var trade = await session.Events.AggregateStreamAsync<TradeOrder>(id, token: ct);
        if (trade is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var cancelled = trade.Cancel(req.Reason, DateTimeOffset.UtcNow);

        await session.Events.AppendOptimistic(id, cancelled);
        await session.SaveChangesAsync(ct);

        var summary = await session.LoadAsync<TradeOrderSummary>(id, ct);
        await Send.OkAsync(summary!, ct);
    }
}
