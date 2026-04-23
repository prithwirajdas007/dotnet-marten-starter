using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;

namespace MartenStarter.Api.Endpoints;

public sealed class GetTradeEndpoint(IQuerySession session)
    : EndpointWithoutRequest<TradeOrder>
{
    public override void Configure()
    {
        Get("/api/trades/{id:guid}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        // Guard against Guid.Empty — Marten treats it as "no stream filter" and will
        // happily replay every event in the table into a single aggregate. Ask me how
        // I know.
        if (id == Guid.Empty)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Live aggregation — Marten replays the full stream on every request to
        // rebuild the aggregate. Fine while streams are tiny; gets expensive fast.
        // TODO phase 3: read from the TradeOrderSummary projection instead.
        var trade = await session.Events.AggregateStreamAsync<TradeOrder>(id, token: ct);

        if (trade is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(trade, ct);
    }
}
