using Alba;
using Marten;
using MartenStarter.Api.Endpoints;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace MartenStarter.Tests.Integration;

// Each test resets the DB in InitializeAsync so state is isolated. Tests use
// Alba scenarios to hit endpoints through the real HTTP pipeline.
[Collection("Integration")]
public class TradeLifecycleIntegrationTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_then_price_then_execute_lands_in_executed_state()
    {
        var id = await CreateTrade("USDCAD", 1_000_000m, "Buy");

        var priced = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { price = 1.37m, currency = "CAD" }).ToUrl($"/api/trades/{id}/price");
            x.StatusCodeShouldBe(200);
        });
        var afterPrice = priced.FromJson<TradeOrderSummary>();
        Assert.Equal(TradeStatus.Priced, afterPrice.Status);
        Assert.Equal(1.37m, afterPrice.QuotedPrice);
        Assert.Equal("CAD", afterPrice.Currency);

        var executed = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { executionPrice = 1.371m, counterparty = "BankX" })
                .ToUrl($"/api/trades/{id}/execute");
            x.StatusCodeShouldBe(200);
        });
        var afterExecute = executed.FromJson<TradeOrderSummary>();
        Assert.Equal(TradeStatus.Executed, afterExecute.Status);
        Assert.Equal(1.371m, afterExecute.ExecutionPrice);
        Assert.Equal("BankX", afterExecute.Counterparty);
    }

    [Fact]
    public async Task Amend_then_cancel_preserves_new_quantity_and_sets_status()
    {
        var id = await CreateTrade("EURUSD", 500_000m, "Sell");

        var amended = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { newQuantity = 750_000m }).ToUrl($"/api/trades/{id}/amend");
            x.StatusCodeShouldBe(200);
        });
        var afterAmend = amended.FromJson<TradeOrderSummary>();
        Assert.Equal(750_000m, afterAmend.Quantity);
        Assert.Equal(TradeStatus.Created, afterAmend.Status);

        var cancelled = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { reason = "client withdrew" }).ToUrl($"/api/trades/{id}/cancel");
            x.StatusCodeShouldBe(200);
        });
        var afterCancel = cancelled.FromJson<TradeOrderSummary>();
        Assert.Equal(TradeStatus.Cancelled, afterCancel.Status);
        Assert.Equal("client withdrew", afterCancel.CancellationReason);
        Assert.Equal(750_000m, afterCancel.Quantity);
    }

    [Fact]
    public async Task Cancelling_an_executed_trade_is_rejected_with_422()
    {
        var id = await CreateTrade("GBPUSD", 100_000m, "Buy");
        await Price(id, 1.25m, "USD");
        await Execute(id, 1.251m, "BankY");

        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { reason = "too late" }).ToUrl($"/api/trades/{id}/cancel");
            x.StatusCodeShouldBe(422);
        });
    }

    [Fact]
    public async Task Executing_an_unpriced_trade_is_rejected_with_422()
    {
        var id = await CreateTrade("USDCAD", 1m, "Buy");

        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { executionPrice = 1m, counterparty = "X" })
                .ToUrl($"/api/trades/{id}/execute");
            x.StatusCodeShouldBe(422);
        });
    }

    [Fact]
    public async Task Mutations_against_an_unknown_id_return_404()
    {
        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { price = 1m, currency = "USD" })
                .ToUrl("/api/trades/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/price");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Zero_quantity_on_create_returns_400()
    {
        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { instrument = "USDCAD", quantity = 0, side = "Buy" }).ToUrl("/api/trades");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Blank_cancel_reason_returns_400()
    {
        var id = await CreateTrade("USDCAD", 1m, "Buy");

        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { reason = "   " }).ToUrl($"/api/trades/{id}/cancel");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Cancelling_a_trade_archives_its_event_stream()
    {
        var id = await CreateTrade("USDCAD", 1m, "Buy");

        await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { reason = "client withdrew" }).ToUrl($"/api/trades/{id}/cancel");
            x.StatusCodeShouldBe(200);
        });

        // Inspect the stream state directly via Marten — the IsArchived flag on
        // mt_streams is what ArchiveStream toggles.
        var store = fixture.Host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession();
        var streamState = await session.Events.FetchStreamStateAsync(id);

        Assert.NotNull(streamState);
        Assert.True(streamState!.IsArchived);
    }

    // ---------- helpers ----------

    private async Task<Guid> CreateTrade(string instrument, decimal quantity, string side)
    {
        var result = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { instrument, quantity, side }).ToUrl("/api/trades");
            x.StatusCodeShouldBe(201);
        });
        return result.FromJson<CreateTradeResponse>().Id;
    }

    private Task Price(Guid id, decimal price, string currency) =>
        fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { price, currency }).ToUrl($"/api/trades/{id}/price");
            x.StatusCodeShouldBe(200);
        });

    private Task Execute(Guid id, decimal executionPrice, string counterparty) =>
        fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { executionPrice, counterparty }).ToUrl($"/api/trades/{id}/execute");
            x.StatusCodeShouldBe(200);
        });
}
