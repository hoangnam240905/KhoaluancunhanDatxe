using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Serializes writers on the current database connection.
/// SQLite has no row-level SELECT FOR UPDATE; BEGIN IMMEDIATE takes a reserved
/// lock so check+write cannot interleave (pessimistic, ACID, no Redis).
/// </summary>
internal static class SqliteWriteLock
{
    public static async Task<T> ExecuteAsync<T>(DbContext db, Func<Task<T>> work)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return await ExecuteSqliteImmediateAsync(db, work);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await work();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<T> ExecuteSqliteImmediateAsync<T>(DbContext db, Func<Task<T>> work)
    {
        var previous = db.Database.AutoTransactionBehavior;
        db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        try
        {
            await using (var timeout = connection.CreateCommand())
            {
                timeout.CommandText = "PRAGMA busy_timeout = 8000;";
                await timeout.ExecuteNonQueryAsync();
            }

            await using (var begin = connection.CreateCommand())
            {
                begin.CommandText = "BEGIN IMMEDIATE;";
                await begin.ExecuteNonQueryAsync();
            }

            try
            {
                var result = await work();
                await using (var commit = connection.CreateCommand())
                {
                    commit.CommandText = "COMMIT;";
                    await commit.ExecuteNonQueryAsync();
                }

                return result;
            }
            catch
            {
                try
                {
                    await using var rollback = connection.CreateCommand();
                    rollback.CommandText = "ROLLBACK;";
                    await rollback.ExecuteNonQueryAsync();
                }
                catch
                {
                    /* already rolled back */
                }

                throw;
            }
        }
        finally
        {
            db.Database.AutoTransactionBehavior = previous;
        }
    }
}
