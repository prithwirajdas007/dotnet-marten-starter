using FastEndpoints;
using Marten;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Events;

namespace MartenStarter.Api.Endpoints;

public record CreateTradeRequest(string Instrument, decimal Quantity, Side Side);
public record CreateTradeResponse(Guid Id, TradeStatus Status);

public sealed class CreateTradeEndpoint(IDocumentSession session)
    : Endpoint<CreateTradeRequest, CreateTradeResponse>
{
    public override void Configure()
    {
        Post("/api/trades");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateTradeRequest req, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var created = TradeOrder.Create(id, req.Instrument, req.Quantity, req.Side, DateTimeOffset.UtcNow);

        // StartStream is how Marten knows this is a brand-new aggregate.
        // Append would fail here because the stream doesn't exist yet.
        session.Events.StartStream<TradeOrder>(id, created);
        await session.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetTradeEndpoint>(
            new { id },
            new CreateTradeResponse(id, TradeStatus.Created),
            generateAbsoluteUrl: true,
            cancellation: ct);
    }
}
