using System;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Dixels;

/// <summary>
/// One throwaway Postgres container per test run, migrated with the real migrations (so the
/// exclusion constraint from Add_Bookings exists exactly as in production). Started before
/// any test class is constructed, which is when the ABP application — and its connection
/// string — is built.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvironmentVariable) != "1")
        {
            return;
        }

        // Same switch as DixelsEntityFrameworkCoreModule — must be set before Npgsql is used.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var options = new DbContextOptionsBuilder<DixelsDbContext>().UseNpgsql(ConnectionString).Options;
        await using var dbContext = new DixelsDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
