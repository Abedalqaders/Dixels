using System;
using Xunit;

namespace Dixels;

/// <summary>
/// A test that needs a real Postgres (started in Docker by <see cref="PostgresFixture"/>).
/// Opt-in: skipped unless <c>DIXELS_POSTGRES_TESTS=1</c>, so a plain <c>dotnet test</c> on
/// a machine without Docker still passes. These cover what the SQLite suite can't: row
/// locks, real transactions and the exclusion constraint.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "DIXELS_POSTGRES_TESTS";

    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            Skip = $"Set {EnvironmentVariable}=1 (Docker required) to run the Postgres tests.";
        }
    }
}
