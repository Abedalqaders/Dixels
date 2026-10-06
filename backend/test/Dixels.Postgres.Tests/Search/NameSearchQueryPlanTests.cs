using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.Users;
using Npgsql;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Dixels.Postgres.Search;

/// <summary>
/// Every list's search matches a name anywhere in it. Two checks per list, against the SQL it
/// really sends: the search is a LIKE on the very column (or lower(...) expression) a trigram
/// index from Add_Name_Search_Indexes covers — a strpos(...) or another expression couldn't use
/// it — and Postgres answers that predicate from the index. Table and whole-index scans are
/// switched off for the EXPLAIN: on a test database's handful of rows Postgres rightly reads
/// everything. Terms need three characters or more (a trigram); shorter ones still read every name.
/// </summary>
[Collection(PostgresCollection.Name)]
public class NameSearchQueryPlanTests : DixelsApplicationTestBase<DixelsPostgresTestModule>
{
    private readonly ITestOutputHelper _output;

    public NameSearchQueryPlanTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Runs <paramref name="search"/> and returns the SQL of the query that carried <paramref name="term"/>.</summary>
    private async Task<string> CapturedSearchSqlAsync(string term, Func<Task> search)
    {
        SqlCapture.Instance.Clear();
        await WithUnitOfWorkAsync(search);

        var sql = SqlCapture.Instance.Commands.Last(c =>
            c.Parameters.Cast<NpgsqlParameter>().Any(p => p.Value is string s && s.Contains(term, StringComparison.OrdinalIgnoreCase))).CommandText;
        _output.WriteLine(sql);
        return sql;
    }

    /// <summary>EXPLAIN of <paramref name="where"/> on <paramref name="table"/>, with @term = '%term%' as the list binds it.</summary>
    private async Task<string> ExplainAsync(string table, string where, string term)
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using (var off = new NpgsqlCommand("SET enable_seqscan = off; SET enable_indexscan = off", connection))
        {
            await off.ExecuteNonQueryAsync();
        }

        await using var explain = new NpgsqlCommand($"EXPLAIN (COSTS OFF) SELECT * FROM \"{table}\" WHERE {where}", connection);
        explain.Parameters.AddWithValue("term", "%" + term + "%");

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        var plan = string.Join(Environment.NewLine, lines);
        _output.WriteLine(plan);
        return plan;
    }

    private async Task ShouldSearchNamesThroughTheIndexAsync(string table, string term, Func<Task> search)
    {
        (await CapturedSearchSqlAsync(term, search)).ShouldMatch("\"NormalizedName\" LIKE @");
        (await ExplainAsync(table, "\"NormalizedName\" LIKE @term", term)).ShouldContain($"IX_{table}_NormalizedName_Trgm");
    }

    [PostgresFact]
    public Task Space_search_uses_the_name_trigram_index() =>
        ShouldSearchNamesThroughTheIndexAsync("AppSpaceTranslations", "ROOM", () =>
            GetRequiredService<ISpacesAppService>().GetListAsync(new GetSpacesInput { Filter = "room", MaxResultCount = 10 }));

    [PostgresFact]
    public Task Building_search_uses_the_name_trigram_index() =>
        ShouldSearchNamesThroughTheIndexAsync("AppBuildingTranslations", "TOWER", () =>
            GetRequiredService<IBuildingsAppService>().GetListAsync(new GetBuildingsInput { Filter = "tower", MaxResultCount = 10 }));

    [PostgresFact]
    public Task Floor_search_uses_the_name_trigram_index() =>
        ShouldSearchNamesThroughTheIndexAsync("AppFloorTranslations", "LEVEL", () =>
            GetRequiredService<IFloorsAppService>().GetListAsync(new GetFloorsInput { Filter = "level", MaxResultCount = 10 }));

    [PostgresFact]
    public Task Space_type_search_uses_the_name_trigram_index() =>
        ShouldSearchNamesThroughTheIndexAsync("AppSpaceTypeTranslations", "DESK", () =>
            GetRequiredService<ISpaceTypesAppService>().GetListAsync(new GetSpaceTypesInput { Filter = "desk", MaxResultCount = 10 }));

    [PostgresFact]
    public async Task User_directory_search_uses_the_lower_case_trigram_indexes()
    {
        var sql = await CapturedSearchSqlAsync("smith", () =>
            GetRequiredService<IUserDirectoryRepository>().GetListAsync("Smith", null, null, null, 0, 10));

        string[] columns = { "UserName", "Name", "Surname", "Email" };
        foreach (var column in columns)
        {
            sql.ShouldMatch($@"lower\(a\.""{column}""\) LIKE @");
        }

        var plan = await ExplainAsync("AbpUsers", string.Join(" OR ", columns.Select(c => $"lower(\"{c}\") LIKE @term")), "smith");
        foreach (var column in columns)
        {
            plan.ShouldContain($"IX_AbpUsers_{column}_Trgm");
        }
    }
}
