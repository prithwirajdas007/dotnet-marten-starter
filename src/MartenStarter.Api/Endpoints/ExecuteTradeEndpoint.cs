using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public record ExecuteTradeRequest(decimal ExecutionPrice, string Counterparty);

public sealed class ExecuteTradeEndpoint(IDocumentSession session)
    : Endpoint<ExecuteTradeRequest, TradeOrderSummary>
{
    public override void Configure()
    {
        Post("/api/trades/{id:guid}/execute");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExecuteTradeRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var trade = await session.Events.AggregateStreamAsync<TradeOrder>(id, token: ct);
        if (trade is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var executed = trade.Execute(req.ExecutionPrice, req.Counterparty, DateTimeOffset.UtcNow);

        await session.Events.AppendOptimistic(id, executed);
        await session.SaveChangesAsync(ct);

        var summary = await session.LoadAsync<TradeOrderSummary>(id, ct);
        await Send.OkAsync(summary!, ct);
    }
}
