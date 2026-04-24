using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public record GetAllTradesRequest(string? Status, string? Instrument);

public sealed class GetAllTradesEndpoint(IQuerySession session)
    : Endpoint<GetAllTradesRequest, IReadOnlyList<TradeOrderSummary>>
{
    // Safety net so a naive call can't pull a huge result set into memory.
    // TODO: swap for real pagination (cursor by LastUpdated + Id) once the repo
    // has an article to point at. Until then, a hard ceiling keeps behaviour honest.
    private const int MaxRows = 100;

    public override void Configure()
    {
        Get("/api/trades");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetAllTradesRequest req, CancellationToken ct)
    {
        IQueryable<TradeOrderSummary> query = session.Query<TradeOrderSummary>();

        // Filter by status if the client asked for a valid one. Silently ignore
        // unknown values rather than 400'ing — the list endpoint should be forgiving.
        if (!string.IsNullOrWhiteSpace(req.Status)
            && Enum.TryParse<TradeStatus>(req.Status, ignoreCase: true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(req.Instrument))
        {
            query = query.Where(x => x.Instrument == req.Instrument);
        }

        var rows = await query
            .OrderByDescending(x => x.LastUpdated)
            .Take(MaxRows)
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
