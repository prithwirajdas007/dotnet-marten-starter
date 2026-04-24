using FastEndpoints;
using Marten;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Api.Endpoints;

public sealed class GetDailyVolumeEndpoint(IQuerySession session)
    : EndpointWithoutRequest<IReadOnlyList<DailyTradeVolume>>
{
    public override void Configure()
    {
        Get("/api/daily-volume");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Straight LINQ against the projection table. Marten translates to SQL,
        // so this is an indexed read, not an event replay.
        var rows = await session.Query<DailyTradeVolume>()
            .OrderByDescending(x => x.TradeDate)
            .ThenBy(x => x.Instrument)
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
