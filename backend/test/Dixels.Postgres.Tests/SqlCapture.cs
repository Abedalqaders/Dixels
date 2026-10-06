using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Dixels;

/// <summary>
/// Records the SQL EF sends (text and parameters), so a test can EXPLAIN exactly the query an
/// app service ran instead of a hand-written copy of it.
/// </summary>
public sealed class SqlCapture : DbCommandInterceptor
{
    public static readonly SqlCapture Instance = new();

    private readonly ConcurrentQueue<NpgsqlCommand> _commands = new();

    public void Clear() => _commands.Clear();

    public IReadOnlyList<NpgsqlCommand> Commands => _commands.ToList();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command)
    {
        if (command is not NpgsqlCommand npgsql)
        {
            return;
        }

        var copy = new NpgsqlCommand(npgsql.CommandText);
        foreach (NpgsqlParameter parameter in npgsql.Parameters)
        {
            copy.Parameters.Add(parameter.Clone());
        }

        _commands.Enqueue(copy);
    }
}
