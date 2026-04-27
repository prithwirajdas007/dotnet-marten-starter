using Alba;
using MartenStarter.Api.Endpoints;
using MartenStarter.Domain.Aggregates;
using MartenStarter.Domain.Projections;

namespace MartenStarter.Tests.Integration;

// These exercise the read side: inline projections written during the POST, then
// queried via the GET endpoints that hit the projection tables directly.
[Collection("Integration")]
public class ProjectionIntegrationTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_by_id_reads_from_summary_projection()
    {
        var id = await CreateTrade("USDCAD", 1_000_000m);

        var result = await fixture.Host.Scenario(x =>
        {
            x.Get.Url($"/api/trades/{id}");
            x.StatusCodeShouldBe(200);
        });
        var summary = result.FromJson<TradeOrderSummary>();

        Assert.Equal(id, summary.Id);
        Assert.Equal("USDCAD", summary.Instrument);
        Assert.Equal(1_000_000m, summary.Quantity);
        Assert.Equal(TradeStatus.Created, summary.Status);
    }

    [Fact]
    public async Task Daily_volume_rolls_up_across_streams_per_instrument()
    {
        // 3 USDCAD + 2 EURUSD — five separate streams, but only two rollup rows.
        for (var i = 0; i < 3; i++)
            await CreateTrade("USDCAD", 1_000_000m);
        for (var i = 0; i < 2; i++)
            await CreateTrade("EURUSD", 500_000m);

        var result = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/daily-volume");
            x.StatusCodeShouldBe(200);
        });
        var rows = result.FromJson<List<DailyTradeVolume>>();

        Assert.Equal(2, rows.Count);
        var usd = rows.Single(r => r.Instrument == "USDCAD");
        var eur = rows.Single(r => r.Instrument == "EURUSD");
        Assert.Equal(3, usd.TradeCount);
        Assert.Equal(3_000_000m, usd.TotalNotional);
        Assert.Equal(2, eur.TradeCount);
        Assert.Equal(1_000_000m, eur.TotalNotional);
    }

    [Fact]
    public async Task List_trades_filters_by_status()
    {
        // Two trades go all the way to Executed, one stays in Created.
        var a = await CreateTrade("USDCAD", 1m);
        await Price(a, 1m, "USD");
        await Execute(a, 1m, "X");

        var b = await CreateTrade("EURUSD", 1m);
        await Price(b, 1m, "USD");
        await Execute(b, 1m, "Y");

        await CreateTrade("GBPUSD", 1m);

        var result = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/trades?status=Executed");
            x.StatusCodeShouldBe(200);
        });
        var list = result.FromJson<List<TradeOrderSummary>>();

        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal(TradeStatus.Executed, t.Status));
    }

    [Fact]
    public async Task List_trades_filters_by_instrument()
    {
        await CreateTrade("USDCAD", 1m);
        await CreateTrade("USDCAD", 2m);
        await CreateTrade("EURUSD", 1m);

        var result = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/trades?instrument=USDCAD");
            x.StatusCodeShouldBe(200);
        });
        var list = result.FromJson<List<TradeOrderSummary>>();

        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal("USDCAD", t.Instrument));
    }

    [Fact]
    public async Task Seed_creates_five_trades_spanning_every_state()
    {
        await fixture.Host.Scenario(x =>
        {
            x.Post.Url("/api/seed");
            x.StatusCodeShouldBe(200);
        });

        var listResult = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/trades");
            x.StatusCodeShouldBe(200);
        });
        var list = listResult.FromJson<List<TradeOrderSummary>>();

        Assert.Equal(5, list.Count);
        Assert.Contains(list, t => t.Status == TradeStatus.Created);
        Assert.Contains(list, t => t.Status == TradeStatus.Priced);
        Assert.Contains(list, t => t.Status == TradeStatus.Executed);
        Assert.Contains(list, t => t.Status == TradeStatus.Cancelled);
    }

    // ---------- helpers ----------

    private async Task<Guid> CreateTrade(string instrument, decimal quantity)
    {
        var result = await fixture.Host.Scenario(x =>
        {
            x.Post.Json(new { instrument, quantity, side = "Buy" }).ToUrl("/api/trades");
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
