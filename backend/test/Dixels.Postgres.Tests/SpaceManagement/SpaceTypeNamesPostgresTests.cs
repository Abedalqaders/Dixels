using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Dixels.SpaceManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace Dixels.Postgres.SpaceManagement;

/// <summary>
/// What only a real Postgres can prove about space type names: the migration that moved
/// Name into the translations table keeps every existing name, and the unique index (not
/// just SpaceTypeManager's check) stops two admins who save the same name at once.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SpaceTypeNamesPostgresTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    /// <summary>The migration just before SpaceTypeNamesToTranslations.</summary>
    private const string MigrationBeforeTranslations = "20260928100619_Add_BookingSeries";

    [PostgresFact]
    public async Task The_migration_keeps_each_existing_name_as_an_English_row()
    {
        // A database of its own, migrated only up to the old schema (Name on AppSpaceTypes).
        var database = "migration_" + Guid.NewGuid().ToString("N")[..8];
        await using (var admin = new NpgsqlConnection(PostgresFixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(PostgresFixture.ConnectionString) { Database = database }.ConnectionString;
        await using var dbContext = new DixelsDbContext(new DbContextOptionsBuilder<DixelsDbContext>().UseNpgsql(connectionString).Options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeTranslations);

        var live = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var sameIgnoringCase = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AppSpaceTypes" ("Id", "Name", "IconKey", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
            VALUES ({0}, 'Phone booth', 'FocusPod', '{{}}', 'x', now(), false),
                   ({1}, 'Old room', 'Generic', '{{}}', 'y', now(), true),
                   ({2}, ' phone BOOTH ', 'Generic', '{{}}', 'z', now() + interval '1 minute', false)
            """,
            live, deleted, sameIgnoringCase);

        await migrator.MigrateAsync();

        var rows = await dbContext.Set<SpaceTypeTranslation>().AsNoTracking().OrderBy(t => t.NormalizedName).ToListAsync();
        rows.Select(t => (t.SpaceTypeId, t.Language, t.Name, t.NormalizedName, t.IsDeleted)).ShouldBe(new[]
        {
            (deleted, "en", "Old room", "OLD ROOM", true),
            (live, "en", "Phone booth", "PHONE BOOTH", false),
            // The old index let these two differ by case; now they'd clash, so the later is numbered.
            (sameIgnoringCase, "en", "phone BOOTH (2)", "PHONE BOOTH (2)", false),
        });
    }

    [PostgresFact]
    public async Task Two_admins_saving_the_same_name_at_once_get_a_duplicate_error_not_a_crash()
    {
        var spaceTypes = GetRequiredService<ISpaceTypesAppService>();
        var name = "Race " + Guid.NewGuid().ToString("N")[..8];

        // Each save in its own transaction — like two HTTP requests — released together.
        var attempts = Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(isTransactional: true), () =>
                spaceTypes.CreateAsync(new CreateSpaceTypeDto { Names = [new() { Language = "en", Name = name }] }))))
            .ToList();

        var outcomes = await Task.WhenAll(attempts.Select(async t =>
        {
            try
            {
                await t;
                return (string?)null;
            }
            catch (BusinessException ex)
            {
                return ex.Code;
            }
        }));

        outcomes.Count(code => code is null).ShouldBe(1);
        outcomes.Where(code => code is not null).ShouldAllBe(code =>
            code == DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists
            || code == DixelsDomainErrorCodes.SpaceTypeNameAlreadyExistsConcurrently);
    }
}
