using FastEndpoints;
using Marten;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public sealed class GetTradeEndpoint(IQuerySession session)
    : EndpointWithoutRequest<TradeOrderSummary>
{
    public override void Configure()
    {
        Get("/api/trades/{id:guid}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        // Single indexed lookup against mt_doc_trade_order_summary — no stream replay.
        // The inline projection kept this row in sync when the events were appended.
        var summary = await session.LoadAsync<TradeOrderSummary>(id, ct);

        if (summary is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(summary, ct);
    }
}
