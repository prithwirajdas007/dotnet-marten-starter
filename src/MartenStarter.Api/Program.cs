// Phase 0: bare host, no Marten or FastEndpoints wired up yet.
// Marten, FastEndpoints and the trade endpoints land in Phase 2 — keeping this
// commit tiny so the diff reads as pure scaffolding.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "dotnet-marten-starter — scaffolding only. Endpoints arrive in Phase 2.");

app.Run();
