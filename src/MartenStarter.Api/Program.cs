using System.Text.Json.Serialization;
using FastEndpoints;
using JasperFx;
using JasperFx.Events.Projections;
using Marten;
using Marten.Exceptions;
using MartenStarter.Domain;
using MartenStarter.Domain.Projections;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured."));

    // Store enums as their names, not ints. Makes the events table actually readable
    // when you're poking around in psql at 2am.
    opts.UseSystemTextJsonForSerialization(EnumStorage.AsString);

    // Dev only. In prod you'd generate migrations ahead of time and review them.
    if (builder.Environment.IsDevelopment())
    {
        opts.AutoCreateSchemaObjects = AutoCreate.All;
    }

    // Inline = updated in the same transaction as the event append. Either both
    // land or neither does, so reads never see a stream ahead of its summary.
    opts.Projections.Add<TradeOrderSummaryProjection>(ProjectionLifecycle.Inline);

    // Cross-stream roll-up: many trade streams fold into a few (date, instrument) docs.
    opts.Projections.Add<DailyTradeVolumeProjection>(ProjectionLifecycle.Inline);
})
// Lightweight sessions skip identity-map tracking — cheaper, and we don't need
// change-tracking for event-sourced writes anyway.
.UseLightweightSessions();

builder.Services.AddFastEndpoints();

var app = builder.Build();

// Turn known domain/argument errors into HTTP codes that actually mean something.
// Anything else falls through to the default 500 so it shows up in logs instead of
// being quietly swallowed.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (DomainException ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await ctx.Response.WriteAsJsonAsync(new { error = "business_rule", message = ex.Message });
    }
    catch (ConcurrencyException ex) when (!ctx.Response.HasStarted)
    {
        // Someone else wrote to this stream between our load and our save.
        // Client should re-read and retry.
        ctx.Response.StatusCode = StatusCodes.Status409Conflict;
        await ctx.Response.WriteAsJsonAsync(new { error = "concurrency", message = ex.Message });
    }
    catch (ArgumentException ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { error = "invalid_argument", message = ex.Message });
    }
});

app.UseFastEndpoints(c =>
{
    // Accept enums as names in request bodies so clients can POST {"side": "Buy"}
    // rather than the much less readable {"side": 0}.
    c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

app.MapGet("/", () => "dotnet-marten-starter is running. Try POST /api/trades.");

app.Run();
