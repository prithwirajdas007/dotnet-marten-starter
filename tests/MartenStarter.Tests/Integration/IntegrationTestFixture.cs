using Alba;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MartenStarter.Tests.Integration;

// Shared across every integration test via IntegrationCollection so Postgres spins
// up exactly once per `dotnet test` run. The container is thrown away when the
// test run ends, so there's no cleanup to worry about.
public class IntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("marten")
            .WithPassword("marten")
            .WithDatabase("marten_starter")
            .Build();

        await _postgres.StartAsync();

        // Env var sits above appsettings in ASP.NET's config hierarchy, so the
        // throwaway Postgres wins over whatever's in appsettings.Development.json.
        // Double-underscore maps to the nested "ConnectionStrings:Postgres" key.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());

        Host = await AlbaHost.For<Program>(builder =>
        {
            // Development env flips AutoCreateSchemaObjects on in Program.cs, so
            // Marten creates every table we need on first use.
            builder.UseEnvironment("Development");
        });
    }

    public async Task DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    // Wipes documents and events between tests so counts are predictable. Keeps the
    // schema in place — no migration penalty per test.
    public async Task ResetAsync()
    {
        var store = Host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.ResetAllData();
    }
}
